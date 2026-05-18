using PaymentGateway.Server.Applications.Models.Dbs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Midtrans.Controllers;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Operations;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class SnapControllerTests
    {
        [Fact]
        public async Task GetPaymentStatus_ReturnsFeeBreakdownInSerializedResponse()
        {
            await using var dbContext = CreateDbContext();
            var seeded = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "settlement");

            var controller = CreateController(
                dbContext,
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))) ,
                new StubReconciliationService(new MidtransTransactionReconciliationResult
                {
                    Transaction = seeded.Transaction,
                    Environment = seeded.Environment,
                    VerifiedStatus = new MidtransVerifiedStatus
                    {
                        TransactionStatus = "settlement",
                        GrossAmount = "10300.00",
                        TransactionId = "midtrans-txn-id",
                        PaymentType = "credit_card",
                        FeeBreakdown = new Dto_SnapFeeBreakdown
                        {
                            FinalGrossAmount = 10300.00m,
                            OriginalAmount = 10000.00m,
                            CustomerPaymentFee = 300.00m,
                            FeePercentage = 3.00m
                        }
                    },
                    RedirectKind = MidtransRedirectKind.Success,
                    StatusResponse = new Dto_SnapStatusResponse
                    {
                        CallerOrderId = seeded.Transaction.CallerOrderId,
                        MidtransOrderId = seeded.Transaction.MidtransOrderId,
                        GatewayStatus = "settlement",
                        MidtransStatus = "settlement",
                        FraudStatus = "accept",
                        GrossAmount = "10300.00",
                        FeeBreakdown = new Dto_SnapFeeBreakdown
                        {
                            FinalGrossAmount = 10300.00m,
                            OriginalAmount = 10000.00m,
                            CustomerPaymentFee = 300.00m,
                            FeePercentage = 3.00m
                        },
                        MidtransTransactionId = "midtrans-txn-id",
                        PaymentType = "credit_card",
                        CreatedAt = seeded.Transaction.CreatedAt,
                        UpdatedAt = seeded.Transaction.UpdatedAt
                    }
                }));

            controller.ControllerContext.HttpContext.Request.Headers["X-Api-Key"] = seeded.Environment.ApiKey;

            var result = await controller.GetPaymentStatus(seeded.Transaction.CallerOrderId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<DataWrapper<Dto_SnapStatusResponse>>(okResult.Value);
            Assert.Equal("Payment status retrieved successfully.", payload.Message);
            Assert.NotNull(payload.Data);
            Assert.NotNull(payload.Data!.FeeBreakdown);

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(okResult.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var feeBreakdown = json.RootElement.GetProperty("data").GetProperty("feeBreakdown");
            Assert.Equal(10300.00m, feeBreakdown.GetProperty("finalGrossAmount").GetDecimal());
            Assert.Equal(300.00m, feeBreakdown.GetProperty("customerPaymentFee").GetDecimal());
        }

        [Fact]
        public async Task CancelPayment_ReturnsFeeBreakdownInSerializedResponse()
        {
            await using var dbContext = CreateDbContext();
            var seeded = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "pending");
                        var verifiedStatusResponse = new Dto_SnapStatusResponse
                        {
                                CallerOrderId = seeded.Transaction.CallerOrderId,
                                MidtransOrderId = seeded.Transaction.MidtransOrderId,
                                GatewayStatus = "cancel",
                                MidtransStatus = "cancel",
                                FraudStatus = null,
                                GrossAmount = "10300.00",
                                FeeBreakdown = new Dto_SnapFeeBreakdown
                                {
                                        FinalGrossAmount = 10300.00m,
                                        OriginalAmount = 10000.00m,
                                        CustomerPaymentFee = 300.00m,
                                        FeePercentage = 3.00m
                                },
                                MidtransTransactionId = "midtrans-txn-id",
                                PaymentType = "credit_card",
                                CreatedAt = seeded.Transaction.CreatedAt,
                                UpdatedAt = seeded.Transaction.UpdatedAt
                        };

            var controller = CreateController(
                dbContext,
                new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "transaction_status": "cancel",
                          "gross_amount": "10300.00",
                          "transaction_id": "midtrans-txn-id",
                                                    "payment_type": "credit_card"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                }))),
                                new StubReconciliationService(new MidtransTransactionReconciliationResult
                                {
                                        Transaction = seeded.Transaction,
                                        Environment = seeded.Environment,
                                        VerifiedStatus = new MidtransVerifiedStatus
                                        {
                                                TransactionStatus = "cancel",
                                                GrossAmount = "10300.00",
                                                TransactionId = "midtrans-txn-id",
                                                PaymentType = "credit_card",
                                                FeeBreakdown = verifiedStatusResponse.FeeBreakdown
                                        },
                                        RedirectKind = MidtransRedirectKind.Failure,
                                        StatusResponse = verifiedStatusResponse
                                }));

            controller.ControllerContext.HttpContext.Request.Headers["X-Api-Key"] = seeded.Environment.ApiKey;

            var result = await controller.CancelPayment(seeded.Transaction.CallerOrderId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<DataWrapper<Dto_SnapStatusResponse>>(okResult.Value);
            Assert.Equal("Payment cancelled successfully.", payload.Message);
            Assert.NotNull(payload.Data);
            Assert.NotNull(payload.Data!.FeeBreakdown);

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(okResult.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var feeBreakdown = json.RootElement.GetProperty("data").GetProperty("feeBreakdown");
            Assert.Equal(10300.00m, feeBreakdown.GetProperty("finalGrossAmount").GetDecimal());
            Assert.Equal(10000.00m, feeBreakdown.GetProperty("originalAmount").GetDecimal());
            Assert.Equal(300.00m, feeBreakdown.GetProperty("customerPaymentFee").GetDecimal());
            Assert.Equal("cancel", payload.Data.GatewayStatus);
        }

        [Fact]
        public void ResolveBrowserCallbackUrl_ReturnsPendingUrl_WhenPendingRedirectHasDedicatedUrl()
        {
            var environment = CreateEnvironment(pendingResponseUrl: "https://payment.advine.id/payment/pending");

            var targetUrl = SnapController.ResolveBrowserCallbackUrl(environment, MidtransRedirectKind.Pending);

            Assert.Equal("https://payment.advine.id/payment/pending", targetUrl);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveBrowserCallbackUrl_FallsBackToFailureUrl_WhenPendingRedirectUrlIsBlank(string? pendingResponseUrl)
        {
            var environment = CreateEnvironment(pendingResponseUrl);

            var targetUrl = SnapController.ResolveBrowserCallbackUrl(environment, MidtransRedirectKind.Pending);

            Assert.Equal(environment.FailureResponseUrl, targetUrl);
        }

        [Fact]
        public void ResolveBrowserCallbackUrl_ReturnsSuccessUrl_ForSuccessRedirect()
        {
            var environment = CreateEnvironment(pendingResponseUrl: "https://payment.advine.id/payment/pending");

            var targetUrl = SnapController.ResolveBrowserCallbackUrl(environment, MidtransRedirectKind.Success);

            Assert.Equal(environment.SuccessResponseUrl, targetUrl);
        }

        private static Db_Environment CreateEnvironment(string? pendingResponseUrl)
        {
            return new Db_Environment
            {
                SuccessResponseUrl = "https://payment.advine.id/payment/success",
                PendingResponseUrl = pendingResponseUrl,
                FailureResponseUrl = "https://payment.advine.id/payment/failed"
            };
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            return new AppDbContext(options);
        }

        private static (Db_Environment Environment, Db_SnapTransaction Transaction) SeedTransaction(
            AppDbContext dbContext,
            bool isSandbox,
            string transactionStatus)
        {
            var application = new Db_Application
            {
                Id = Guid.NewGuid(),
                Name = "Test App",
                UserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var environment = new Db_Environment
            {
                Id = Guid.NewGuid(),
                ApplicationId = application.Id,
                Name = isSandbox ? "staging" : "production",
                ApiKey = Guid.NewGuid().ToString("N"),
                SuccessResponseUrl = "https://payment.advine.id/payment/success",
                PendingResponseUrl = "https://payment.advine.id/payment/pending",
                FailureResponseUrl = "https://payment.advine.id/payment/failed",
                IsSandbox = isSandbox,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var transaction = new Db_SnapTransaction
            {
                Id = Guid.NewGuid(),
                EnvironmentId = environment.Id,
                CallerOrderId = "order-001",
                MidtransOrderId = "abcd1234_order-001",
                GrossAmount = 10000,
                MidtransEnv = isSandbox ? "sandbox" : "production",
                TransactionStatus = transactionStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Applications.Add(application);
            dbContext.Environments.Add(environment);
            dbContext.SnapTransactions.Add(transaction);
            dbContext.SaveChanges();

            return (environment, transaction);
        }

        private static SnapController CreateController(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IMidtransTransactionReconciliationService reconciliationService)
        {
            return new SnapController(
                dbContext,
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
                httpClientFactory,
                NullLogger<SnapController>.Instance,
                null!,
                null!,
                new StubSecurityMetricsService(),
                reconciliationService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        private sealed class StubReconciliationService(MidtransTransactionReconciliationResult? reconciliationResult)
            : IMidtransTransactionReconciliationService
        {
            public Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
                string midtransOrderId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(reconciliationResult);
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
    }
}