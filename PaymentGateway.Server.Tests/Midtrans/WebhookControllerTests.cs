using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Midtrans.Controllers;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Midtrans.Utils;
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.Webhook;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
// WebhookForwardRetryOptions is in PaymentGateway.Server.Midtrans.Models (no extra using needed — already included above)

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class WebhookControllerTests
    {
        [Fact]
        public async Task SandboxWebhook_AcknowledgesOldValidSettlementNotification()
        {
            const string orderId = "order-old";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "sandbox-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-30)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-old",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            var replayGuard = new ConfigurableWebhookReplayGuard(_ => true);
            var reconciliationService = new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook"));
            var controller = CreateController(
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))),
                reconciliationService,
                replayGuard: replayGuard);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.SandboxWebhook();

            Assert.IsType<OkResult>(result);
            Assert.Equal(1, reconciliationService.CallCount);
            Assert.Equal(1, replayGuard.CallCount);
        }

        [Fact]
        public async Task SandboxWebhook_ReturnsBadRequest_ForInvalidSignature()
        {
            const string orderId = "order-invalid-signature";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "bad-signature",
              "transaction_status": "settlement",
              "transaction_id": "txn-invalid-signature",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            var replayGuard = new ConfigurableWebhookReplayGuard(_ => true);
            var reconciliationService = new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook"));
            var controller = CreateController(
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Forwarding should not run for invalid signatures.")))),
                reconciliationService,
                replayGuard: replayGuard);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.SandboxWebhook();

            Assert.IsType<BadRequestResult>(result);
            Assert.Equal(0, reconciliationService.CallCount);
            Assert.Equal(0, replayGuard.CallCount);
        }

        [Fact]
        public async Task SandboxWebhook_ReturnsBadRequest_ForFutureTransactionTime()
        {
            const string orderId = "order-future";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "sandbox-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(30)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-future",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            var replayGuard = new ConfigurableWebhookReplayGuard(_ => true);
            var reconciliationService = new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook"));
            var controller = CreateController(
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Forwarding should not run for future-skewed notifications.")))),
                reconciliationService,
                replayGuard: replayGuard);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.SandboxWebhook();

            Assert.IsType<BadRequestResult>(result);
            Assert.Equal(0, reconciliationService.CallCount);
            Assert.Equal(0, replayGuard.CallCount);
        }

        [Fact]
        public async Task SandboxWebhook_AcknowledgesDuplicateNotification_WithoutReprocessing()
        {
            const string orderId = "order-duplicate";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "sandbox-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-duplicate",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            var replayGuard = new ConfigurableWebhookReplayGuard(_ => false);
            var reconciliationService = new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook"));
            var controller = CreateController(
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Forwarding should not run for duplicate notifications.")))),
                reconciliationService,
                replayGuard: replayGuard);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.SandboxWebhook();

            Assert.IsType<OkResult>(result);
            Assert.Equal(0, reconciliationService.CallCount);
            Assert.Equal(1, replayGuard.CallCount);
        }

        [Fact]
        public async Task ProductionWebhook_ForwardsEnrichedPayload_WhileSignatureVerificationUsesTopLevelGrossAmount()
        {
            const string orderId = "order-123";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "production-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-123",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            string? forwardedBody = null;
            var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            {
                using var requestBodyStream = request.Content!.ReadAsStream();
                using var reader = new StreamReader(requestBodyStream, Encoding.UTF8);
                forwardedBody = reader.ReadToEnd();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));

            var controller = CreateController(
                new StubHttpClientFactory(httpClient),
                new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook")));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.ProductionWebhook();

            Assert.IsType<OkResult>(result);
            Assert.NotNull(forwardedBody);

            using var forwardedDocument = JsonDocument.Parse(forwardedBody!);
            var root = forwardedDocument.RootElement;

            Assert.Equal(topLevelGrossAmount, root.GetProperty("gross_amount").GetString());

            var gatewayFeeBreakdown = root.GetProperty("gateway_fee_breakdown");
            Assert.Equal(10300.00m, gatewayFeeBreakdown.GetProperty("final_gross_amount").GetDecimal());
            Assert.Equal(10000.00m, gatewayFeeBreakdown.GetProperty("original_amount").GetDecimal());
            Assert.Equal(300.00m, gatewayFeeBreakdown.GetProperty("customer_payment_fee").GetDecimal());
            Assert.Equal(3.00m, gatewayFeeBreakdown.GetProperty("fee_percentage").GetDecimal());
        }

        [Fact]
        public async Task ProductionWebhook_ForwardsOriginalPayloadWithNullGatewayFeeBreakdown_WhenReconciliationHasNoFeeBreakdown()
        {
            const string orderId = "order-123";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "production-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-123",
              "transaction_time": "{{transactionTime}}",
              "payment_type": "bank_transfer"
            }
            """;

            string? forwardedBody = null;
            var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            {
                using var requestBodyStream = request.Content!.ReadAsStream();
                using var reader = new StreamReader(requestBodyStream, Encoding.UTF8);
                forwardedBody = reader.ReadToEnd();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));

            var controller = CreateController(
                new StubHttpClientFactory(httpClient),
                new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook", includeFeeBreakdown: false)));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.ProductionWebhook();

            Assert.IsType<OkResult>(result);
            Assert.NotNull(forwardedBody);

            using var forwardedDocument = JsonDocument.Parse(forwardedBody!);
            var root = forwardedDocument.RootElement;

            Assert.Equal(orderId, root.GetProperty("order_id").GetString());
            Assert.Equal(statusCode, root.GetProperty("status_code").GetString());
            Assert.Equal(topLevelGrossAmount, root.GetProperty("gross_amount").GetString());
            Assert.Equal("settlement", root.GetProperty("transaction_status").GetString());
            Assert.Equal("txn-123", root.GetProperty("transaction_id").GetString());
            Assert.Equal("bank_transfer", root.GetProperty("payment_type").GetString());

            var gatewayFeeBreakdown = root.GetProperty("gateway_fee_breakdown");
            Assert.Equal(JsonValueKind.Null, gatewayFeeBreakdown.ValueKind);
        }

        /// <summary>
        /// When the inline forward fails (non-2xx), the controller must still return 200 to Midtrans
        /// and must have called EnqueueAsync (leaving a Pending row for the background drainer to retry).
        /// MarkDeliveredAsync must NOT be called — the row stays retryable.
        /// </summary>
        [Fact]
        public async Task SandboxWebhook_Returns200_AndEnqueuesRetryableRow_WhenInlineForwardFails()
        {
            const string orderId = "order-fail";
            const string statusCode = "200";
            const string topLevelGrossAmount = "10000.00";
            const string serverKey = "sandbox-server-key";
            var signatureKey = CreateSignature(orderId, statusCode, topLevelGrossAmount, serverKey);
            var transactionTime = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(7))
                .AddMinutes(-1)
                .ToString("yyyy-MM-dd HH:mm:ss");

            var rawBody = $$"""
            {
              "order_id": "{{orderId}}",
              "status_code": "{{statusCode}}",
              "gross_amount": "{{topLevelGrossAmount}}",
              "signature_key": "{{signatureKey}}",
              "transaction_status": "settlement",
              "transaction_id": "txn-fail",
              "transaction_time": "{{transactionTime}}"
            }
            """;

            // HTTP client always returns 503 — simulates the child app being down
            var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            var trackingService = new TrackingWebhookForwardService();

            var controller = CreateController(
                new StubHttpClientFactory(httpClient),
                new StubReconciliationService(CreateReconciliationResult("https://8.8.8.8/webhook")),
                webhookForwardService: trackingService);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
            controller.HttpContext.Request.ContentType = "application/json";

            var result = await controller.SandboxWebhook();

            // Controller must acknowledge to Midtrans regardless of inline forward outcome
            Assert.IsType<OkResult>(result);
            // EnqueueAsync was called — row is in outbox for retry
            Assert.True(trackingService.EnqueueCalled, "EnqueueAsync must be called even when inline forward fails");
            // MarkDeliveredAsync must NOT have been called — the row stays Pending for the drainer
            Assert.False(trackingService.MarkDeliveredCalled, "MarkDeliveredAsync must not be called when forward failed");
        }

        private static WebhookController CreateController(
            IHttpClientFactory httpClientFactory,
            IMidtransTransactionReconciliationService reconciliationService,
            WebhookHardeningOptions? hardeningOptions = null,
            IWebhookReplayGuard? replayGuard = null,
            IWebhookUrlSafetyValidator? urlSafetyValidator = null,
            IWebhookForwardService? webhookForwardService = null)
        {
            return new WebhookController(
                Options.Create(new MidtransOptions
                {
                    Production = new MidtransEnvironmentOptions
                    {
                        IsEnabled = true,
                        ServerKey = "production-server-key"
                    },
                    Sandbox = new MidtransEnvironmentOptions
                    {
                        IsEnabled = true,
                        ServerKey = "sandbox-server-key"
                    }
                }),
                Options.Create(hardeningOptions ?? new WebhookHardeningOptions
                {
                    ForwardRetryCount = 0,
                    ForwardRetryDelayMs = 50,
                    RejectWhenTransactionTimeMissing = false,
                    ReplayWindowMinutes = 15,
                    DeduplicationWindowMinutes = 60
                }),
                Options.Create(new WebhookForwardRetryOptions()),
                httpClientFactory,
                replayGuard ?? new ConfigurableWebhookReplayGuard(_ => true),
                new StubSecurityMetricsService(),
                reconciliationService,
                urlSafetyValidator ?? new AlwaysSafeWebhookUrlSafetyValidator(),
                webhookForwardService ?? new NoOpWebhookForwardService(),
                NullLogger<WebhookController>.Instance);
        }

        private static MidtransTransactionReconciliationResult CreateReconciliationResult(
            string webhookUrl,
            bool includeFeeBreakdown = true)
        {
            var environmentId = Guid.NewGuid();
            var feeBreakdown = includeFeeBreakdown
                ? new Dto_SnapFeeBreakdown
                {
                    FinalGrossAmount = 10300.00m,
                    OriginalAmount = 10000.00m,
                    CustomerPaymentFee = 300.00m,
                    FeePercentage = 3.00m
                }
                : null;

            return new MidtransTransactionReconciliationResult
            {
                Transaction = new Db_SnapTransaction
                {
                    EnvironmentId = environmentId,
                    MidtransOrderId = "order-123",
                    CallerOrderId = "caller-order-123",
                    TransactionStatus = "settlement",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                Environment = new Db_Environment
                {
                    Id = environmentId,
                    Name = "production",
                    WebhookUrl = webhookUrl,
                    SuccessResponseUrl = "https://example.com/success",
                    FailureResponseUrl = "https://example.com/failure",
                    PendingResponseUrl = "https://example.com/pending"
                },
                VerifiedStatus = new MidtransVerifiedStatus
                {
                    TransactionStatus = "settlement",
                    GrossAmount = "10300.00",
                    TransactionId = "verified-txn-123",
                    FeeBreakdown = feeBreakdown
                },
                RedirectKind = MidtransRedirectKind.Success,
                StatusResponse = new Dto_SnapStatusResponse()
            };
        }

        private static string CreateSignature(string orderId, string statusCode, string grossAmount, string serverKey)
        {
            var raw = orderId + statusCode + grossAmount + serverKey;
            return Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }

        private sealed class StubReconciliationService(MidtransTransactionReconciliationResult reconciliationResult)
            : IMidtransTransactionReconciliationService
        {
            public int CallCount { get; private set; }

            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult<MidtransTransactionReconciliationResult?>(reconciliationResult);
            }
        }

        private sealed class ConfigurableWebhookReplayGuard(Func<string, bool> tryAcquire)
            : IWebhookReplayGuard
        {
            public int CallCount { get; private set; }

            public bool TryAcquire(string dedupeKey, TimeSpan ttl)
            {
                CallCount++;
                return tryAcquire(dedupeKey);
            }
        }

        private sealed class StubSecurityMetricsService : ISecurityMetricsService
        {
            public void Increment(string metricName, string? dimension = null)
            {
            }

            public IReadOnlyList<SecurityMetricSnapshot> GetSnapshots() => Array.Empty<SecurityMetricSnapshot>();
        }

        private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => httpClient;
        }

        private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(responseFactory(request));
            }
        }

        /// <summary>Stub that approves every URL — mirrors the real validator's behaviour for public IPs like 8.8.8.8.</summary>
        private sealed class AlwaysSafeWebhookUrlSafetyValidator : IWebhookUrlSafetyValidator
        {
            public Task<bool> IsWebhookUrlSafeAsync(string url) => Task.FromResult(true);
        }

        /// <summary>
        /// Stub forward service: builds the payload faithfully (so forwarded-body-content tests can verify it)
        /// but does not persist to any database.
        /// </summary>
        private sealed class NoOpWebhookForwardService : IWebhookForwardService
        {
            public Task<Db_WebhookForwardOutbox> EnqueueAsync(
                Guid snapTransactionId,
                Guid environmentId,
                string midtransOrderId,
                string callerOrderId,
                string targetUrl,
                string rawBody,
                MidtransVerifiedStatus verifiedStatus,
                int maxAttempts,
                CancellationToken cancellationToken = default)
            {
                // Build the payload the same way the real service does so content-inspection tests pass
                var payload = MidtransWebhookForwardPayloadBuilder.Build(rawBody, verifiedStatus.FeeBreakdown);
                var row = new Db_WebhookForwardOutbox
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    SnapTransactionId = snapTransactionId,
                    MidtransOrderId = midtransOrderId,
                    CallerOrderId = callerOrderId,
                    TargetUrl = targetUrl,
                    Payload = payload,
                    RawNotificationBody = rawBody,
                    Status = WebhookForwardStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                return Task.FromResult(row);
            }

            public Task TryDeliverAsync(
                Db_WebhookForwardOutbox row,
                WebhookForwardRetryOptions options,
                CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task MarkDeliveredAsync(
                Guid snapTransactionId,
                int statusCode,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        /// <summary>
        /// Tracking forward service: records which methods were called so tests can assert on call patterns.
        /// </summary>
        private sealed class TrackingWebhookForwardService : IWebhookForwardService
        {
            public bool EnqueueCalled { get; private set; }
            public bool MarkDeliveredCalled { get; private set; }
            public bool TryDeliverCalled { get; private set; }

            public Task<Db_WebhookForwardOutbox> EnqueueAsync(
                Guid snapTransactionId,
                Guid environmentId,
                string midtransOrderId,
                string callerOrderId,
                string targetUrl,
                string rawBody,
                MidtransVerifiedStatus verifiedStatus,
                int maxAttempts,
                CancellationToken cancellationToken = default)
            {
                EnqueueCalled = true;
                var payload = MidtransWebhookForwardPayloadBuilder.Build(rawBody, verifiedStatus.FeeBreakdown);
                var row = new Db_WebhookForwardOutbox
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    SnapTransactionId = snapTransactionId,
                    MidtransOrderId = midtransOrderId,
                    CallerOrderId = callerOrderId,
                    TargetUrl = targetUrl,
                    Payload = payload,
                    RawNotificationBody = rawBody,
                    Status = WebhookForwardStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                return Task.FromResult(row);
            }

            public Task TryDeliverAsync(
                Db_WebhookForwardOutbox row,
                WebhookForwardRetryOptions options,
                CancellationToken cancellationToken = default)
            {
                TryDeliverCalled = true;
                return Task.CompletedTask;
            }

            public Task MarkDeliveredAsync(
                Guid snapTransactionId,
                int statusCode,
                CancellationToken cancellationToken = default)
            {
                MarkDeliveredCalled = true;
                return Task.CompletedTask;
            }
        }
    }
}