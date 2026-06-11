using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Webhook;
using System.Net;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class WebhookForwardServiceTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(options);
        }

        private static WebhookForwardService CreateService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IWebhookUrlSafetyValidator? urlSafetyValidator = null,
            WebhookForwardRetryOptions? options = null)
        {
            return new WebhookForwardService(
                dbContext,
                httpClientFactory,
                urlSafetyValidator ?? new AlwaysSafeValidator(),
                NullLogger<WebhookForwardService>.Instance,
                Options.Create(options ?? DefaultOptions()));
        }

        private static WebhookForwardRetryOptions DefaultOptions() => new()
        {
            BaseBackoffSeconds = 30,
            MaxBackoffSeconds = 3600,
            MaxAttempts = 3,
            BatchSize = 50,
            IntervalSeconds = 30
        };

        private static MidtransVerifiedStatus FakeVerifiedStatus() => new()
        {
            TransactionStatus = "settlement",
            GrossAmount = "10000.00",
            TransactionId = "txn-abc",
            FeeBreakdown = new Dto_SnapFeeBreakdown
            {
                FinalGrossAmount = 10300m,
                OriginalAmount = 10000m,
                CustomerPaymentFee = 300m,
                FeePercentage = 3m
            }
        };

        /// <summary>Raw body that MidtransWebhookForwardPayloadBuilder.Build can parse.</summary>
        private const string RawBody = """{"order_id":"ord-1","transaction_status":"settlement","gross_amount":"10000.00","transaction_id":"txn-abc"}""";

        private static Db_WebhookForwardOutbox MakeOutboxRow(
            AppDbContext dbContext,
            string status,
            int attemptCount,
            DateTime? nextAttemptAt = null)
        {
            var row = new Db_WebhookForwardOutbox
            {
                Id = Guid.NewGuid(),
                EnvironmentId = Guid.NewGuid(),
                SnapTransactionId = Guid.NewGuid(),
                MidtransOrderId = "ord-1",
                CallerOrderId = "caller-ord-1",
                TargetUrl = "https://example.com/webhook",
                Payload = """{"order_id":"ord-1"}""",
                Status = status,
                AttemptCount = attemptCount,
                MaxAttempts = 3,
                NextAttemptAt = nextAttemptAt ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.WebhookForwardOutbox.Add(row);
            dbContext.SaveChanges();
            return row;
        }

        // ── EnqueueAsync tests ────────────────────────────────────────────────

        [Fact]
        public async Task EnqueueAsync_CreatesExactlyOnePendingRow_ForNewTransaction()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, NoOpHttpClientFactory());

            var snapTxId = Guid.NewGuid();
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 3);

            var rows = await db.WebhookForwardOutbox.ToListAsync();
            Assert.Single(rows);
            Assert.Equal(WebhookForwardStatus.Pending, rows[0].Status);
            Assert.Equal(snapTxId, rows[0].SnapTransactionId);
        }

        [Fact]
        public async Task EnqueueAsync_Upserts_WhenCalledAgainForSameTransaction()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, NoOpHttpClientFactory());

            var snapTxId = Guid.NewGuid();

            // First call
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 3);

            // Second call for the same transaction — should upsert, not insert
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh/updated",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 5);

            var rows = await db.WebhookForwardOutbox.ToListAsync();
            Assert.Single(rows); // still exactly one row
            Assert.Equal(WebhookForwardStatus.Pending, rows[0].Status);
            Assert.Equal("https://example.com/wh/updated", rows[0].TargetUrl);
            Assert.Equal(5, rows[0].MaxAttempts);
        }

        // ── TryDeliverAsync — 2xx (success) ──────────────────────────────────

        [Fact]
        public async Task TryDeliverAsync_Delivered_On2xxResponse()
        {
            await using var db = CreateDbContext();
            var httpFactory = StubHttpClientFactory(HttpStatusCode.OK);
            var svc = CreateService(db, httpFactory);

            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            await svc.TryDeliverAsync(row, DefaultOptions());

            Assert.Equal(WebhookForwardStatus.Delivered, row.Status);
            Assert.Equal(1, row.AttemptCount);
            Assert.Equal(200, row.LastResponseCode);
            Assert.Null(row.NextAttemptAt);
        }

        [Theory]
        [InlineData(HttpStatusCode.Created)]     // 201
        [InlineData(HttpStatusCode.NoContent)]   // 204
        public async Task TryDeliverAsync_Delivered_OnAny2xxResponse(HttpStatusCode code)
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(code));
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            await svc.TryDeliverAsync(row, DefaultOptions());

            Assert.Equal(WebhookForwardStatus.Delivered, row.Status);
            Assert.Equal((int)code, row.LastResponseCode);
        }

        // ── TryDeliverAsync — 5xx (failure + backoff) ────────────────────────

        [Fact]
        public async Task TryDeliverAsync_OnFailure_IncrementsAttemptCount_AndSetsNextAttemptAt()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            var before = DateTime.UtcNow;
            await svc.TryDeliverAsync(row, DefaultOptions());
            var after = DateTime.UtcNow;

            Assert.Equal(WebhookForwardStatus.Failed, row.Status);
            Assert.Equal(1, row.AttemptCount);
            Assert.Equal(500, row.LastResponseCode);
            Assert.NotNull(row.NextAttemptAt);
            Assert.True(row.NextAttemptAt > after, "NextAttemptAt should be in the future");
        }

        [Fact]
        public async Task TryDeliverAsync_BackoffGrows_ExponentiallyWithAttemptCount()
        {
            var opts = new WebhookForwardRetryOptions
            {
                BaseBackoffSeconds = 30,
                MaxBackoffSeconds = 3600,
                MaxAttempts = 10
            };

            // Simulate multiple consecutive failures and collect NextAttemptAt deltas
            double? previousDelta = null;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                await using var db = CreateDbContext();
                var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
                var row = MakeOutboxRow(db, WebhookForwardStatus.Failed, attemptCount: attempt);

                var before = DateTime.UtcNow;
                await svc.TryDeliverAsync(row, opts);

                var delta = (row.NextAttemptAt!.Value - before).TotalSeconds;

                if (previousDelta.HasValue)
                {
                    // Each successive backoff should be >= the previous one (growing or capped)
                    Assert.True(delta >= previousDelta.Value,
                        $"Backoff at attempt {attempt} ({delta:F1}s) should be >= previous ({previousDelta:F1}s)");
                }

                previousDelta = delta;
            }
        }

        [Fact]
        public async Task TryDeliverAsync_BackoffIsCappedAtMaxBackoffSeconds()
        {
            var opts = new WebhookForwardRetryOptions
            {
                BaseBackoffSeconds = 30,
                MaxBackoffSeconds = 60,   // low cap so we hit it fast
                MaxAttempts = 20
            };

            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
            // Start at attempt 10 — 30 * 2^10 = 30720s >> 60s cap
            var row = MakeOutboxRow(db, WebhookForwardStatus.Failed, attemptCount: 10);

            var before = DateTime.UtcNow;
            await svc.TryDeliverAsync(row, opts);

            var delta = (row.NextAttemptAt!.Value - before).TotalSeconds;
            Assert.True(delta <= opts.MaxBackoffSeconds + 2, // +2s tolerance for test execution time
                $"Backoff delta ({delta:F1}s) exceeds MaxBackoffSeconds ({opts.MaxBackoffSeconds}s)");
        }

        // ── TryDeliverAsync — Exhausted after MaxAttempts ────────────────────

        [Fact]
        public async Task TryDeliverAsync_MarksExhausted_WhenAttemptCountReachesMaxAttempts()
        {
            var opts = new WebhookForwardRetryOptions
            {
                BaseBackoffSeconds = 30,
                MaxBackoffSeconds = 3600,
                MaxAttempts = 3
            };

            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
            // Row that is already at MaxAttempts - 1 (so this attempt tips it over)
            var row = MakeOutboxRow(db, WebhookForwardStatus.Failed, attemptCount: 2);

            await svc.TryDeliverAsync(row, opts);

            Assert.Equal(WebhookForwardStatus.Exhausted, row.Status);
            Assert.Equal(3, row.AttemptCount);
            Assert.Null(row.NextAttemptAt);
        }

        [Fact]
        public async Task TryDeliverAsync_NoFurtherRetryScheduled_WhenExhausted()
        {
            var opts = new WebhookForwardRetryOptions
            {
                MaxAttempts = 1
            };

            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.ServiceUnavailable));
            var row = MakeOutboxRow(db, WebhookForwardStatus.Failed, attemptCount: 0);

            await svc.TryDeliverAsync(row, opts);

            Assert.Equal(WebhookForwardStatus.Exhausted, row.Status);
            Assert.Null(row.NextAttemptAt);
        }

        // ── TryDeliverAsync — SSRF guard ─────────────────────────────────────

        [Fact]
        public async Task TryDeliverAsync_SsrfRejection_DoesNotCallHttpAndRowIsNotDelivered()
        {
            await using var db = CreateDbContext();
            bool httpCalled = false;
            var factory = TrackingHttpClientFactory(() =>
            {
                httpCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var svc = CreateService(db, factory, urlSafetyValidator: new AlwaysUnsafeValidator());
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            await svc.TryDeliverAsync(row, DefaultOptions());

            Assert.False(httpCalled, "HTTP handler must not be called when SSRF check fails");
            Assert.NotEqual(WebhookForwardStatus.Delivered, row.Status);
        }

        [Fact]
        public async Task TryDeliverAsync_SsrfRejection_IncrementsAttemptCountAndSetsError()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, NoOpHttpClientFactory(), urlSafetyValidator: new AlwaysUnsafeValidator());
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            await svc.TryDeliverAsync(row, DefaultOptions());

            Assert.Equal(1, row.AttemptCount);
            Assert.NotNull(row.LastError);
            Assert.Contains("SSRF", row.LastError, StringComparison.OrdinalIgnoreCase);
        }

        // ── EnqueueAsync — H-3: terminal row idempotency ──────────────────────

        [Fact]
        public async Task EnqueueAsync_DoesNotResetDeliveredRow_WhenPayloadIsIdentical()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, NoOpHttpClientFactory());

            var snapTxId = Guid.NewGuid();

            // First enqueue
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 3);

            // Manually mark as Delivered
            var row = await db.WebhookForwardOutbox.SingleAsync();
            row.Status = WebhookForwardStatus.Delivered;
            await db.SaveChangesAsync();

            // Second enqueue with identical payload — must NOT reset
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 3);

            var updatedRow = await db.WebhookForwardOutbox.SingleAsync();
            // Must stay Delivered — identical payload must not re-arm
            Assert.Equal(WebhookForwardStatus.Delivered, updatedRow.Status);
        }

        [Fact]
        public async Task EnqueueAsync_ReArmsDeliveredRow_WhenPayloadHasChanged()
        {
            await using var db = CreateDbContext();
            var svc = CreateService(db, NoOpHttpClientFactory());

            var snapTxId = Guid.NewGuid();

            // First enqueue with settlement body
            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: RawBody,
                verifiedStatus: FakeVerifiedStatus(),
                maxAttempts: 3);

            // Manually mark as Delivered
            var row = await db.WebhookForwardOutbox.SingleAsync();
            row.Status = WebhookForwardStatus.Delivered;
            await db.SaveChangesAsync();

            // Second enqueue with a DIFFERENT status in the raw body — simulates a genuine status transition
            const string newRawBody = """{"order_id":"ord-1","transaction_status":"refund","gross_amount":"10000.00","transaction_id":"txn-abc"}""";
            var newVerifiedStatus = new MidtransVerifiedStatus
            {
                TransactionStatus = "refund",
                GrossAmount = "10000.00",
                TransactionId = "txn-abc"
            };

            await svc.EnqueueAsync(
                snapTransactionId: snapTxId,
                environmentId: Guid.NewGuid(),
                midtransOrderId: "ord-1",
                callerOrderId: "caller-1",
                targetUrl: "https://example.com/wh",
                rawBody: newRawBody,
                verifiedStatus: newVerifiedStatus,
                maxAttempts: 3);

            var updatedRow = await db.WebhookForwardOutbox.SingleAsync();
            // Must be re-armed to Pending — payload changed
            Assert.Equal(WebhookForwardStatus.Pending, updatedRow.Status);
        }

        // ── MarkDeliveredAsync ────────────────────────────────────────────────

        [Fact]
        public async Task MarkDeliveredAsync_SetsDeliveredWithoutHttpCall()
        {
            await using var db = CreateDbContext();
            bool httpCalled = false;
            var factory = TrackingHttpClientFactory(() =>
            {
                httpCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var svc = CreateService(db, factory);
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            await svc.MarkDeliveredAsync(row.SnapTransactionId, 200);

            Assert.False(httpCalled, "MarkDeliveredAsync must not make any HTTP call");

            var updated = await db.WebhookForwardOutbox.SingleAsync();
            Assert.Equal(WebhookForwardStatus.Delivered, updated.Status);
            Assert.Equal(1, updated.AttemptCount);
            Assert.Equal(200, updated.LastResponseCode);
            Assert.Null(updated.NextAttemptAt);
        }

        // ── Backoff first-retry timing ─────────────────────────────────────────

        [Fact]
        public async Task TryDeliverAsync_FirstRetryBackoffIsApproximately30s()
        {
            // After first failure (AttemptCount goes 0 → 1), backoff formula is
            // Base * 2^(AttemptCount-1) = 30 * 2^0 = 30s
            var opts = new WebhookForwardRetryOptions
            {
                BaseBackoffSeconds = 30,
                MaxBackoffSeconds = 3600,
                MaxAttempts = 8
            };

            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
            var row = MakeOutboxRow(db, WebhookForwardStatus.Pending, attemptCount: 0);

            var before = DateTime.UtcNow;
            await svc.TryDeliverAsync(row, opts);
            var after = DateTime.UtcNow;

            Assert.Equal(WebhookForwardStatus.Failed, row.Status);
            Assert.Equal(1, row.AttemptCount);
            Assert.NotNull(row.NextAttemptAt);

            // NextAttemptAt should be ~30s from now (Base * 2^0 = 30)
            var delta = (row.NextAttemptAt!.Value - before).TotalSeconds;
            Assert.True(delta >= 28 && delta <= 35,
                $"First retry delay should be ~30s, got {delta:F1}s");
        }

        [Fact]
        public async Task TryDeliverAsync_SecondRetryBackoffIsApproximately60s()
        {
            // After second failure (AttemptCount goes 1 → 2), backoff formula is
            // Base * 2^(AttemptCount-1) = 30 * 2^1 = 60s
            var opts = new WebhookForwardRetryOptions
            {
                BaseBackoffSeconds = 30,
                MaxBackoffSeconds = 3600,
                MaxAttempts = 8
            };

            await using var db = CreateDbContext();
            var svc = CreateService(db, StubHttpClientFactory(HttpStatusCode.InternalServerError));
            var row = MakeOutboxRow(db, WebhookForwardStatus.Failed, attemptCount: 1);

            var before = DateTime.UtcNow;
            await svc.TryDeliverAsync(row, opts);
            var after = DateTime.UtcNow;

            Assert.Equal(WebhookForwardStatus.Failed, row.Status);
            Assert.Equal(2, row.AttemptCount);
            Assert.NotNull(row.NextAttemptAt);

            var delta = (row.NextAttemptAt!.Value - before).TotalSeconds;
            Assert.True(delta >= 58 && delta <= 65,
                $"Second retry delay should be ~60s, got {delta:F1}s");
        }

        // ── Stubs ─────────────────────────────────────────────────────────────

        private static IHttpClientFactory NoOpHttpClientFactory() =>
            StubHttpClientFactory(HttpStatusCode.OK); // never called in SSRF tests

        private static IHttpClientFactory StubHttpClientFactory(HttpStatusCode code) =>
            new StubHttpFactory(new HttpClient(new StubHandler(_ => new HttpResponseMessage(code))));

        private static IHttpClientFactory TrackingHttpClientFactory(Func<HttpResponseMessage> responseFactory) =>
            new StubHttpFactory(new HttpClient(new StubHandler(_ => responseFactory())));

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

        private sealed class AlwaysUnsafeValidator : IWebhookUrlSafetyValidator
        {
            public Task<bool> IsWebhookUrlSafeAsync(string url) => Task.FromResult(false);
        }
    }
}
