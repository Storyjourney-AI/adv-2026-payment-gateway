using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Webhook;
using System.Net;
using System.Net.Http;

namespace PaymentGateway.Server.Tests.Midtrans
{
    /// <summary>
    /// Tests for <see cref="WebhookForwardRetryService"/>.
    /// Strategy: build a real <see cref="IServiceScope"/> via <see cref="ServiceCollection"/> that
    /// resolves a scoped <see cref="AppDbContext"/> (in-memory) and <see cref="IWebhookForwardService"/>
    /// (real implementation with a stub HTTP factory), then invoke <see cref="WebhookForwardRetryService"/>
    /// via <see cref="BackgroundService.StartAsync"/> + cancellation so exactly one drain cycle fires.
    ///
    /// Each SeedDueRow call also seeds the required Db_Application, Db_Environment, and Db_SnapTransaction
    /// so the drainer's soft-delete join query (which excludes soft-deleted environments/applications) can match.
    /// </summary>
    public class WebhookForwardRetryServiceTests
    {
        private static WebhookForwardRetryOptions FastOptions(int maxAttempts = 3, int inProgressLeaseSeconds = 300) => new()
        {
            IntervalSeconds = 1,
            BaseBackoffSeconds = 30,
            MaxBackoffSeconds = 3600,
            MaxAttempts = maxAttempts,
            BatchSize = 50,
            InProgressLeaseSeconds = inProgressLeaseSeconds
        };

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a scoped DI container where the <see cref="AppDbContext"/> is an EF in-memory database
        /// and <see cref="IWebhookForwardService"/> uses the real implementation wired to a stub HTTP handler.
        /// Returns the root <see cref="ServiceProvider"/> so the test can seed data before running the service.
        /// </summary>
        private static ServiceProvider BuildServiceProvider(
            Func<HttpRequestMessage, HttpResponseMessage> httpHandler,
            WebhookForwardRetryOptions? options = null)
        {
            var dbName = Guid.NewGuid().ToString("N");
            var services = new ServiceCollection();

            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);

            // Singleton HTTP factory pointing at the stub handler
            var httpClient = new HttpClient(new StubHandler(httpHandler));
            services.AddSingleton<IHttpClientFactory>(new StubHttpFactory(httpClient));

            // Always-safe validator so SSRF doesn't interfere with drain tests
            services.AddSingleton<IWebhookUrlSafetyValidator, AlwaysSafeValidator>();

            var resolvedOptions = options ?? FastOptions();
            services.AddSingleton<IOptions<WebhookForwardRetryOptions>>(Options.Create(resolvedOptions));

            services.AddScoped<IWebhookForwardService, WebhookForwardService>(sp =>
                new WebhookForwardService(
                    sp.GetRequiredService<AppDbContext>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<IWebhookUrlSafetyValidator>(),
                    NullLogger<WebhookForwardService>.Instance,
                    sp.GetRequiredService<IOptions<WebhookForwardRetryOptions>>()));

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Seeds a complete Application → Environment → SnapTransaction → WebhookForwardOutbox chain.
        /// The drainer joins against SnapTransaction (with Environment + Application) to enforce
        /// soft-delete filtering, so all related rows must exist in the in-memory DB.
        /// </summary>
        private static Db_WebhookForwardOutbox SeedDueRow(
            AppDbContext db,
            string status = WebhookForwardStatus.Pending,
            int attemptCount = 0,
            DateTime? nextAttemptAt = null,
            bool environmentDeleted = false,
            bool applicationDeleted = false)
        {
            var app = new Db_Application
            {
                Id = Guid.NewGuid(),
                Name = "Test App",
                UserId = Guid.NewGuid(),
                IsDeleted = applicationDeleted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var env = new Db_Environment
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                Name = "production",
                ApiKey = Guid.NewGuid().ToString("N"),
                WebhookUrl = "https://example.com/webhook",
                SuccessResponseUrl = "https://example.com/ok",
                PendingResponseUrl = "https://example.com/pending",
                FailureResponseUrl = "https://example.com/fail",
                IsSandbox = false,
                IsDeleted = environmentDeleted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var tx = new Db_SnapTransaction
            {
                Id = Guid.NewGuid(),
                EnvironmentId = env.Id,
                CallerOrderId = "caller-retry",
                MidtransOrderId = "ord-retry",
                GrossAmount = 10000,
                MidtransEnv = "production",
                TransactionStatus = "settlement",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var row = new Db_WebhookForwardOutbox
            {
                Id = Guid.NewGuid(),
                EnvironmentId = env.Id,
                SnapTransactionId = tx.Id,
                MidtransOrderId = "ord-retry",
                CallerOrderId = "caller-retry",
                TargetUrl = "https://example.com/webhook",
                Payload = """{"order_id":"ord-retry"}""",
                RawNotificationBody = """{"order_id":"ord-retry"}""",
                Status = status,
                AttemptCount = attemptCount,
                MaxAttempts = 3,
                NextAttemptAt = nextAttemptAt ?? DateTime.UtcNow.AddSeconds(-1), // due now
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Use IgnoreQueryFilters to allow seeding soft-deleted rows for negative tests
            db.Applications.Add(app);
            db.Environments.Add(env);
            db.SnapTransactions.Add(tx);
            db.WebhookForwardOutbox.Add(row);
            db.SaveChanges();
            return row;
        }

        // ── drain cycle tests ─────────────────────────────────────────────────

        [Fact]
        public async Task DrainCycle_DeliversDueRow_WhenEndpointReturns2xx()
        {
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            // Seed a due Pending row
            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedDueRow(db);
            }

            // Run exactly one drain tick
            await RunOneDrainCycleAsync(provider, FastOptions());

            // Verify the row is now Delivered
            using var verifyScope = provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await verifyDb.WebhookForwardOutbox.SingleAsync();

            Assert.Equal(WebhookForwardStatus.Delivered, row.Status);
            Assert.Equal(1, row.AttemptCount);
        }

        [Fact]
        public async Task DrainCycle_ProcessesFailedRow_WhenStatusIsFailed()
        {
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedDueRow(db, status: WebhookForwardStatus.Failed, attemptCount: 1);
            }

            await RunOneDrainCycleAsync(provider, FastOptions());

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db2.WebhookForwardOutbox.SingleAsync();

            Assert.Equal(WebhookForwardStatus.Delivered, row.Status);
        }

        [Fact]
        public async Task DrainCycle_LeavesNotYetDueRow_Untouched()
        {
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Row scheduled 1 hour in the future — not due
                SeedDueRow(db, nextAttemptAt: DateTime.UtcNow.AddHours(1));
            }

            await RunOneDrainCycleAsync(provider, FastOptions());

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db2.WebhookForwardOutbox.SingleAsync();

            // Must remain untouched
            Assert.Equal(WebhookForwardStatus.Pending, row.Status);
            Assert.Equal(0, row.AttemptCount);
        }

        [Fact]
        public async Task DrainCycle_ProcessesDueRow_ButIgnoresNonDueRow_InSameBatch()
        {
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedDueRow(db); // due
                SeedDueRow(db, nextAttemptAt: DateTime.UtcNow.AddHours(2)); // not due
            }

            await RunOneDrainCycleAsync(provider, FastOptions());

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db2.WebhookForwardOutbox.OrderBy(r => r.NextAttemptAt ?? DateTime.MaxValue).ToListAsync();

            Assert.Equal(2, rows.Count);
            var delivered = rows.Count(r => r.Status == WebhookForwardStatus.Delivered);
            var pending = rows.Count(r => r.Status == WebhookForwardStatus.Pending);
            Assert.Equal(1, delivered);
            Assert.Equal(1, pending);
        }

        // ── concurrency: InProgress row is not picked up by a second drain pass ──

        [Fact]
        public async Task DrainCycle_SkipsInProgressRow_WhenLeaseIsNotExpired()
        {
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Guid seededRowId;
            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Seed as InProgress with LastAttemptAt = now (lease active)
                var row = SeedDueRow(db,
                    status: WebhookForwardStatus.InProgress,
                    nextAttemptAt: DateTime.UtcNow.AddSeconds(-1));
                row.LastAttemptAt = DateTime.UtcNow; // lease is fresh
                db.SaveChanges();
                seededRowId = row.Id;
            }

            // Drain should skip the InProgress row (lease not expired)
            await RunOneDrainCycleAsync(provider, FastOptions());

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row2 = await db2.WebhookForwardOutbox.SingleAsync(r => r.Id == seededRowId);

            // Row must still be InProgress — not picked up by the drainer
            Assert.Equal(WebhookForwardStatus.InProgress, row2.Status);
        }

        // ── stale-lease reclaim: InProgress rows past the lease window ARE reclaimed ──

        [Fact]
        public async Task DrainCycle_ReclaimsStaleInProgressRow_WhenLeaseHasExpired()
        {
            // Use a very short lease (1 s) so we can mark LastAttemptAt in the past without sleeping.
            var shortLeaseOptions = FastOptions(inProgressLeaseSeconds: 1);
            await using var provider = BuildServiceProvider(
                _ => new HttpResponseMessage(HttpStatusCode.OK),
                shortLeaseOptions);

            Guid seededRowId;
            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Seed as InProgress with LastAttemptAt well past the 1-second lease window.
                var row = SeedDueRow(db,
                    status: WebhookForwardStatus.InProgress,
                    nextAttemptAt: DateTime.UtcNow.AddSeconds(-1));
                row.LastAttemptAt = DateTime.UtcNow.AddSeconds(-60); // 60s ago — stale
                db.SaveChanges();
                seededRowId = row.Id;
            }

            await RunOneDrainCycleAsync(provider, shortLeaseOptions);

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row2 = await db2.WebhookForwardOutbox.SingleAsync(r => r.Id == seededRowId);

            // Drainer must have reclaimed the stale row and delivered it.
            Assert.Equal(WebhookForwardStatus.Delivered, row2.Status);
        }

        [Fact]
        public async Task DrainCycle_SkipsFreshInProgressRow_WhenLeaseHasNotExpired()
        {
            // Use a long lease (300 s) so a row with LastAttemptAt = now is NOT reclaimable.
            var longLeaseOptions = FastOptions(inProgressLeaseSeconds: 300);
            await using var provider = BuildServiceProvider(
                _ => new HttpResponseMessage(HttpStatusCode.OK),
                longLeaseOptions);

            Guid seededRowId;
            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Seed as InProgress with LastAttemptAt = now (well within the 300-second lease).
                var row = SeedDueRow(db,
                    status: WebhookForwardStatus.InProgress,
                    nextAttemptAt: DateTime.UtcNow.AddSeconds(-1));
                row.LastAttemptAt = DateTime.UtcNow; // fresh lease
                db.SaveChanges();
                seededRowId = row.Id;
            }

            await RunOneDrainCycleAsync(provider, longLeaseOptions);

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row2 = await db2.WebhookForwardOutbox.SingleAsync(r => r.Id == seededRowId);

            // Row must still be InProgress — lease not expired, drainer must not touch it.
            Assert.Equal(WebhookForwardStatus.InProgress, row2.Status);
        }

        // ── soft-delete: drainer skips rows for deleted environments ────────────

        [Fact]
        public async Task DrainCycle_SkipsDueRow_WhenEnvironmentIsSoftDeleted()
        {
            // The HasQueryFilter on Db_Environment(!IsDeleted) means EF in-memory excludes soft-deleted rows
            // from the SnapTransactions navigation-property join — so the drainer's join returns nothing
            // for outbox rows whose environment is deleted, which is the correct behavior.
            await using var provider = BuildServiceProvider(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using (var seedScope = provider.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedDueRow(db, environmentDeleted: true);
            }

            await RunOneDrainCycleAsync(provider, FastOptions());

            using var verifyScope = provider.CreateScope();
            var db2 = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Query without filter to check raw status
            var row = await db2.WebhookForwardOutbox.SingleAsync();

            // Row must remain Pending — drainer should have skipped it
            Assert.Equal(WebhookForwardStatus.Pending, row.Status);
            Assert.Equal(0, row.AttemptCount);
        }

        // ── helper: run exactly one drain tick ────────────────────────────────

        private static async Task RunOneDrainCycleAsync(ServiceProvider provider, WebhookForwardRetryOptions options)
        {
            // Use a CancellationTokenSource that cancels after the first interval elapses
            // so the BackgroundService loop executes exactly one drain cycle then stops.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.IntervalSeconds + 2));

            var service = new WebhookForwardRetryService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(options),
                NullLogger<WebhookForwardRetryService>.Instance);

            // Start fires ExecuteAsync which runs one drain then awaits Task.Delay(interval).
            // Cancel mid-delay to avoid waiting the full interval.
            await service.StartAsync(cts.Token);

            // Give the drain cycle a moment to finish before we cancel
            await Task.Delay(300, CancellationToken.None);

            cts.Cancel();

            try
            {
                await service.StopAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Expected — the background task was cancelled while waiting in Task.Delay
            }
        }

        // ── stubs ─────────────────────────────────────────────────────────────

        private sealed class StubHttpFactory(HttpClient client) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => client;
        }

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(fn(request));
        }

        private sealed class AlwaysSafeValidator : IWebhookUrlSafetyValidator
        {
            public Task<bool> IsWebhookUrlSafeAsync(string url) => Task.FromResult(true);
        }
    }
}
