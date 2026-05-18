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
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.Webhook;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class WebhookControllerTests
    {
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

        private static WebhookController CreateController(
            IHttpClientFactory httpClientFactory,
            IMidtransTransactionReconciliationService reconciliationService)
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
                Options.Create(new WebhookHardeningOptions
                {
                    ForwardRetryCount = 0,
                    ForwardRetryDelayMs = 50,
                    RejectWhenTransactionTimeMissing = false,
                    ReplayWindowMinutes = 15,
                    DeduplicationWindowMinutes = 60
                }),
                httpClientFactory,
                new AllowAllWebhookReplayGuard(),
                new StubSecurityMetricsService(),
                reconciliationService,
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
            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<MidtransTransactionReconciliationResult?>(reconciliationResult);
            }
        }

        private sealed class AllowAllWebhookReplayGuard : IWebhookReplayGuard
        {
            public bool TryAcquire(string dedupeKey, TimeSpan ttl) => true;
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
    }
}