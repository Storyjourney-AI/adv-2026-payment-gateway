using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Controllers;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Webhook;
using System.Net;
using System.Security.Claims;

namespace PaymentGateway.Server.Tests.Midtrans
{
    /// <summary>
    /// Tests for <c>POST /api/transaction/{id}/webhook/retry</c>.
    /// </summary>
    public class TransactionControllerWebhookRetryTests
    {
        // ── infrastructure ────────────────────────────────────────────────────

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(options);
        }

        private static TransactionController CreateController(
            AppDbContext dbContext,
            StubUserManager userManager,
            IMidtransTransactionReconciliationService reconciliationService,
            IWebhookForwardService? forwardService = null,
            ClaimsPrincipal? user = null)
        {
            var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var httpFactory = new StubHttpFactory(httpClient);

            // Real forward service backed by in-memory DB + always-safe SSRF validator
            forwardService ??= new WebhookForwardService(
                dbContext,
                httpFactory,
                new AlwaysSafeValidator(),
                NullLogger<WebhookForwardService>.Instance,
                Options.Create(new WebhookForwardRetryOptions()));

            var controller = new TransactionController(
                dbContext,
                userManager,
                NullLogger<TransactionController>.Instance,
                Options.Create(new MidtransOptions
                {
                    Production = new MidtransEnvironmentOptions { IsEnabled = true, ServerKey = "prod-key" },
                    Sandbox = new MidtransEnvironmentOptions { IsEnabled = true, ServerKey = "sandbox-key" }
                }),
                Options.Create(new WebhookForwardRetryOptions
                {
                    MaxAttempts = 3,
                    BaseBackoffSeconds = 30,
                    MaxBackoffSeconds = 3600
                }),
                httpFactory,
                forwardService,
                reconciliationService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user ?? new ClaimsPrincipal()
                    }
                }
            };

            return controller;
        }

        /// <summary>Seeds application + environment + transaction. Returns their IDs.</summary>
        private static (Guid UserId, Guid EnvId, Guid TxId) SeedTransaction(
            AppDbContext db,
            string webhookUrl = "https://example.com/webhook")
        {
            var userId = Guid.NewGuid();
            var app = new Db_Application
            {
                Id = Guid.NewGuid(),
                Name = "Test App",
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var env = new Db_Environment
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                Name = "production",
                ApiKey = Guid.NewGuid().ToString("N"),
                WebhookUrl = webhookUrl,
                SuccessResponseUrl = "https://example.com/success",
                PendingResponseUrl = "https://example.com/pending",
                FailureResponseUrl = "https://example.com/fail",
                IsSandbox = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var tx = new Db_SnapTransaction
            {
                Id = Guid.NewGuid(),
                EnvironmentId = env.Id,
                CallerOrderId = "caller-001",
                MidtransOrderId = "ord-001",
                GrossAmount = 10000,
                MidtransEnv = "production",
                TransactionStatus = "settlement",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Applications.Add(app);
            db.Environments.Add(env);
            db.SnapTransactions.Add(tx);
            db.SaveChanges();

            return (userId, env.Id, tx.Id);
        }

        private static ClaimsPrincipal MakeUser(Guid userId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub_id", userId.ToString())
            }, "Test");
            return new ClaimsPrincipal(identity);
        }

        private static MidtransTransactionReconciliationResult FakeReconciliationResult(
            Db_SnapTransaction tx,
            Db_Environment env)
        {
            return new MidtransTransactionReconciliationResult
            {
                Transaction = tx,
                Environment = env,
                VerifiedStatus = new MidtransVerifiedStatus
                {
                    TransactionStatus = "settlement",
                    GrossAmount = "10000.00",
                    TransactionId = "txn-midtrans-id",
                    FeeBreakdown = null
                },
                RedirectKind = MidtransRedirectKind.Success,
                StatusResponse = new Dto_SnapStatusResponse()
            };
        }

        // ── 401 — no token ────────────────────────────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns401_WhenNoSubIdClaim()
        {
            await using var db = CreateDbContext();
            var (_, _, txId) = SeedTransaction(db);

            var controller = CreateController(
                db,
                userManager: StubUserManager.NotFound(),
                reconciliationService: new NeverCalledReconciliationService(),
                user: new ClaimsPrincipal() /* no claims */);

            var result = await controller.RetryWebhookForward(txId);

            var statusResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(statusResult.Value);
            Assert.False(wrapper.Success);
        }

        // ── 403 — authenticated but not the owner ────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns403_WhenAuthenticatedNonOwner()
        {
            await using var db = CreateDbContext();
            var (_, _, txId) = SeedTransaction(db);

            var differentUserId = Guid.NewGuid(); // not the app owner
            var user = MakeUser(differentUserId);

            // UserManager returns a non-super-admin user with the different user ID
            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = differentUserId,
                UserName = "other@test.com",
                Email = "other@test.com"
            });

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: new NeverCalledReconciliationService(),
                user: user);

            var result = await controller.RetryWebhookForward(txId);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, statusResult.StatusCode);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(statusResult.Value);
            Assert.False(wrapper.Success);
        }

        // ── 404 — unknown transaction id ─────────────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns404_ForUnknownTransactionId()
        {
            await using var db = CreateDbContext();
            var userId = Guid.NewGuid();
            var user = MakeUser(userId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: new NeverCalledReconciliationService(),
                user: user);

            var unknownId = Guid.NewGuid();
            var result = await controller.RetryWebhookForward(unknownId);

            var statusResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(statusResult.Value);
            Assert.False(wrapper.Success);
        }

        // ── 200 — owner happy path ────────────────────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns200WithDeliveredStatus_ForOwner()
        {
            await using var db = CreateDbContext();
            var (userId, envId, txId) = SeedTransaction(db);
            var user = MakeUser(userId);

            var tx = await db.SnapTransactions.Include(t => t.Environment).FirstAsync(t => t.Id == txId);
            var env = await db.Environments.FirstAsync(e => e.Id == envId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var reconciliation = new StaticReconciliationService(FakeReconciliationResult(tx, env));

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: reconciliation,
                user: user);

            var result = await controller.RetryWebhookForward(txId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(okResult.Value);
            Assert.True(wrapper.Success);
            Assert.NotNull(wrapper.Data);
            Assert.Equal(txId, wrapper.Data!.SnapTransactionId);
            // HTTP handler is always-OK, so delivery should succeed
            Assert.Equal(WebhookForwardStatus.Delivered, wrapper.Data.Status);
            Assert.Equal(1, reconciliation.CallCount);
        }

        // ── 200 — Super Admin happy path ──────────────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns200_ForSuperAdmin_RegardlessOfOwnership()
        {
            await using var db = CreateDbContext();
            var (ownerUserId, envId, txId) = SeedTransaction(db);

            var adminId = Guid.NewGuid(); // not the app owner
            var user = MakeUser(adminId);

            var tx = await db.SnapTransactions.Include(t => t.Environment).FirstAsync(t => t.Id == txId);
            var env = await db.Environments.FirstAsync(e => e.Id == envId);

            var userManager = StubUserManager.SuperAdmin(new Db_ApplicationUser
            {
                Id = adminId,
                UserName = "admin@test.com",
                Email = "admin@test.com"
            });

            var reconciliation = new StaticReconciliationService(FakeReconciliationResult(tx, env));

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: reconciliation,
                user: user);

            var result = await controller.RetryWebhookForward(txId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(okResult.Value);
            Assert.True(wrapper.Success);
            Assert.NotNull(wrapper.Data);
            Assert.Equal(WebhookForwardStatus.Delivered, wrapper.Data!.Status);
        }

        // ── re-verify: reconciliation service is called ───────────────────────

        [Fact]
        public async Task RetryWebhookForward_CallsReconciliationService_BeforeForwarding()
        {
            await using var db = CreateDbContext();
            var (userId, envId, txId) = SeedTransaction(db);
            var user = MakeUser(userId);

            var tx = await db.SnapTransactions.Include(t => t.Environment).FirstAsync(t => t.Id == txId);
            var env = await db.Environments.FirstAsync(e => e.Id == envId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var reconciliation = new StaticReconciliationService(FakeReconciliationResult(tx, env));

            var controller = CreateController(db, userManager, reconciliation, user: user);

            await controller.RetryWebhookForward(txId);

            Assert.Equal(1, reconciliation.CallCount);
        }

        // ── outbox row is created and reflects final state ─────────────────────

        [Fact]
        public async Task RetryWebhookForward_CreatesOutboxRow_WithExpectedFields()
        {
            await using var db = CreateDbContext();
            var (userId, envId, txId) = SeedTransaction(db);
            var user = MakeUser(userId);

            var tx = await db.SnapTransactions.Include(t => t.Environment).FirstAsync(t => t.Id == txId);
            var env = await db.Environments.FirstAsync(e => e.Id == envId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var controller = CreateController(
                db,
                userManager,
                new StaticReconciliationService(FakeReconciliationResult(tx, env)),
                user: user);

            await controller.RetryWebhookForward(txId);

            var row = await db.WebhookForwardOutbox.FirstOrDefaultAsync(o => o.SnapTransactionId == txId);
            Assert.NotNull(row);
            Assert.Equal(txId, row!.SnapTransactionId);
            Assert.Equal(WebhookForwardStatus.Delivered, row.Status);
        }

        // ── 502 — Midtrans re-verification fails ─────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns502_WhenReconciliationThrows()
        {
            await using var db = CreateDbContext();
            var (userId, _, txId) = SeedTransaction(db);
            var user = MakeUser(userId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: new ThrowingReconciliationService(),
                user: user);

            var result = await controller.RetryWebhookForward(txId);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(statusResult.Value);
            Assert.False(wrapper.Success);
        }

        // ── 400 — no WebhookUrl on environment ───────────────────────────────

        [Fact]
        public async Task RetryWebhookForward_Returns400_WhenNoWebhookUrlRegistered()
        {
            await using var db = CreateDbContext();
            // Seed transaction with no webhook URL on the environment
            var (userId, _, txId) = SeedTransaction(db, webhookUrl: "");
            var user = MakeUser(userId);

            var userManager = StubUserManager.Regular(new Db_ApplicationUser
            {
                Id = userId,
                UserName = "owner@test.com",
                Email = "owner@test.com"
            });

            var controller = CreateController(
                db,
                userManager: userManager,
                reconciliationService: new NeverCalledReconciliationService(),
                user: user);

            var result = await controller.RetryWebhookForward(txId);

            var statusResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var wrapper = Assert.IsType<DataWrapper<Dto_WebhookForwardStatus>>(statusResult.Value);
            Assert.False(wrapper.Success);
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

        private sealed class NeverCalledReconciliationService : IMidtransTransactionReconciliationService
        {
            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("ReconcileByMidtransOrderIdAsync must not be called in this test.");
            }
        }

        /// <summary>
        /// Simulates a Midtrans API failure during manual retry re-verification,
        /// causing the controller to return 502 Bad Gateway.
        /// </summary>
        private sealed class ThrowingReconciliationService : IMidtransTransactionReconciliationService
        {
            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                throw new MidtransStatusVerificationException(
                    "Midtrans API returned 500.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        private sealed class StaticReconciliationService(MidtransTransactionReconciliationResult? result)
            : IMidtransTransactionReconciliationService
        {
            public int CallCount { get; private set; }

            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Minimal stub for <see cref="UserManager{TUser}"/> that supports
        /// <see cref="FindByIdAsync"/> and <see cref="IsInRoleAsync"/> without a real store.
        /// </summary>
        private sealed class StubUserManager : UserManager<Db_ApplicationUser>
        {
            private readonly Db_ApplicationUser? m_user;
            private readonly bool m_isSuperAdmin;

            private StubUserManager(Db_ApplicationUser? user, bool isSuperAdmin)
                : base(
                    new NoOpUserStore(),
                    null!, null!, null!, null!, null!, null!, null!, NullLogger<UserManager<Db_ApplicationUser>>.Instance)
            {
                m_user = user;
                m_isSuperAdmin = isSuperAdmin;
            }

            public static StubUserManager NotFound() => new(null, false);
            public static StubUserManager Regular(Db_ApplicationUser user) => new(user, false);
            public static StubUserManager SuperAdmin(Db_ApplicationUser user) => new(user, true);

            public override Task<Db_ApplicationUser?> FindByIdAsync(string userId)
                => Task.FromResult(m_user);

            public override Task<bool> IsInRoleAsync(Db_ApplicationUser user, string role)
                => Task.FromResult(m_isSuperAdmin && role == "Super Admin");

            // Minimal no-op user store — only FindByIdAsync / IsInRoleAsync are exercised
            private sealed class NoOpUserStore
                : IUserStore<Db_ApplicationUser>,
                  IUserRoleStore<Db_ApplicationUser>
            {
                public Task<IdentityResult> CreateAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
                public Task<IdentityResult> DeleteAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
                public Task<Db_ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult<Db_ApplicationUser?>(null);
                public Task<Db_ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => Task.FromResult<Db_ApplicationUser?>(null);
                public Task<string?> GetNormalizedUserNameAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult<string?>(null);
                public Task<string> GetUserIdAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id.ToString());
                public Task<string?> GetUserNameAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
                public Task SetNormalizedUserNameAsync(Db_ApplicationUser user, string? normalizedName, CancellationToken ct) => Task.CompletedTask;
                public Task SetUserNameAsync(Db_ApplicationUser user, string? userName, CancellationToken ct) => Task.CompletedTask;
                public Task<IdentityResult> UpdateAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
                public void Dispose() { }

                // IUserRoleStore
                public Task AddToRoleAsync(Db_ApplicationUser user, string roleName, CancellationToken ct) => Task.CompletedTask;
                public Task RemoveFromRoleAsync(Db_ApplicationUser user, string roleName, CancellationToken ct) => Task.CompletedTask;
                public Task<IList<string>> GetRolesAsync(Db_ApplicationUser user, CancellationToken ct) => Task.FromResult<IList<string>>(new List<string>());
                public Task<bool> IsInRoleAsync(Db_ApplicationUser user, string roleName, CancellationToken ct) => Task.FromResult(false);
                public Task<IList<Db_ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct) => Task.FromResult<IList<Db_ApplicationUser>>(new List<Db_ApplicationUser>());
            }
        }
    }
}
