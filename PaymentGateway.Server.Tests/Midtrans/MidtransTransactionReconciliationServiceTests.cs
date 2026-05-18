using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Services;
using System.Net;
using System.Text;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class MidtransTransactionReconciliationServiceTests
    {
        [Fact]
        public async Task ReconcileByMidtransOrderIdAsync_UpdatesTransactionAndReturnsSuccessRedirect()
        {
            await using var dbContext = CreateDbContext();
            var transaction = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "pending");
            var service = CreateService(dbContext, _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "transaction_status": "settlement",
                      "fraud_status": "accept",
                      "gross_amount": "10000.00",
                      "transaction_id": "txn-success",
                      "payment_type": "bank_transfer",
                      "status_code": "200",
                      "status_message": "OK"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

            var result = await service.ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId);

            Assert.NotNull(result);
            Assert.Equal(MidtransRedirectKind.Success, result!.RedirectKind);
            Assert.Equal("settlement", result.Transaction.TransactionStatus);
            Assert.Equal("txn-success", result.Transaction.MidtransTransactionId);
            Assert.Equal("settlement", result.StatusResponse.MidtransStatus);
        }

        [Fact]
        public async Task ReconcileByMidtransOrderIdAsync_ReturnsPendingRedirectForPendingStatus()
        {
            await using var dbContext = CreateDbContext();
            var transaction = SeedTransaction(dbContext, isSandbox: false, transactionStatus: "pending");
            var service = CreateService(dbContext, _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "transaction_status": "pending",
                      "gross_amount": "10000.00",
                      "transaction_id": "txn-pending",
                      "payment_type": "gopay",
                      "status_code": "201",
                      "status_message": "Pending"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

            var result = await service.ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId);

            Assert.NotNull(result);
            Assert.Equal(MidtransRedirectKind.Pending, result!.RedirectKind);
            Assert.Equal("pending", result.Transaction.TransactionStatus);
            Assert.Equal("txn-pending", result.Transaction.MidtransTransactionId);
        }

        [Fact]
        public async Task ReconcileByMidtransOrderIdAsync_ThrowsWhenMidtransVerificationFails()
        {
            await using var dbContext = CreateDbContext();
            var transaction = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "pending");
            var service = CreateService(dbContext, _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    """
                    {
                      "status_message": "Transaction not found"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

            var exception = await Assert.ThrowsAsync<MidtransStatusVerificationException>(
                () => service.ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId));

            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        }

                [Fact]
                public async Task ReconcileByMidtransOrderIdAsync_ParsesFeeBreakdownWhenGrossAmountInfoExists()
                {
                        await using var dbContext = CreateDbContext();
                        var transaction = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "pending");
                        var service = CreateService(dbContext, _ => new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(
                                        """
                                        {
                                            "transaction_status": "settlement",
                                            "fraud_status": "accept",
                                            "gross_amount": "10300.00",
                                            "transaction_id": "txn-fee-breakdown",
                                            "payment_type": "bank_transfer",
                                            "status_code": "200",
                                            "status_message": "OK",
                                            "metadata": {
                                                "extra_info": {
                                                    "gross_amount_info": {
                                                        "original_amount": "10000.00",
                                                        "customer_payment_fee": "300.00",
                                                        "fee_percentage": "3.00"
                                                    }
                                                }
                                            }
                                        }
                                        """,
                                        Encoding.UTF8,
                                        "application/json")
                        });

                        var result = await service.ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId);

                        Assert.NotNull(result);
                        Assert.Equal("10300.00", result!.StatusResponse.GrossAmount);
                        Assert.NotNull(result.StatusResponse.FeeBreakdown);
                        Assert.Equal(10300.00m, result.StatusResponse.FeeBreakdown!.FinalGrossAmount);
                        Assert.Equal(10000.00m, result.StatusResponse.FeeBreakdown.OriginalAmount);
                        Assert.Equal(300.00m, result.StatusResponse.FeeBreakdown.CustomerPaymentFee);
                        Assert.Equal(3.00m, result.StatusResponse.FeeBreakdown.FeePercentage);
                }

                [Fact]
                public async Task ReconcileByMidtransOrderIdAsync_FallsBackToTopLevelGrossAmountWhenGrossAmountInfoIsAbsent()
                {
                        await using var dbContext = CreateDbContext();
                        var transaction = SeedTransaction(dbContext, isSandbox: true, transactionStatus: "pending");
                        var service = CreateService(dbContext, _ => new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(
                                        """
                                        {
                                            "transaction_status": "settlement",
                                            "fraud_status": "accept",
                                            "gross_amount": "10000.00",
                                            "transaction_id": "txn-no-fee-breakdown",
                                            "payment_type": "bank_transfer",
                                            "status_code": "200",
                                            "status_message": "OK"
                                        }
                                        """,
                                        Encoding.UTF8,
                                        "application/json")
                        });

                        var result = await service.ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId);

                        Assert.NotNull(result);
                        Assert.Equal("10000.00", result!.StatusResponse.GrossAmount);
                        Assert.NotNull(result.StatusResponse.FeeBreakdown);
                        Assert.Equal(10000.00m, result.StatusResponse.FeeBreakdown!.FinalGrossAmount);
                        Assert.Null(result.StatusResponse.FeeBreakdown.OriginalAmount);
                        Assert.Null(result.StatusResponse.FeeBreakdown.CustomerPaymentFee);
                        Assert.Null(result.StatusResponse.FeeBreakdown.FeePercentage);
                }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            return new AppDbContext(options);
        }

        private static Db_SnapTransaction SeedTransaction(AppDbContext dbContext, bool isSandbox, string transactionStatus)
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

            return transaction;
        }

        private static MidtransTransactionReconciliationService CreateService(
            AppDbContext dbContext,
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            var options = Options.Create(new MidtransOptions
            {
                Sandbox = new MidtransEnvironmentOptions
                {
                    ServerKey = "sandbox-server-key",
                    IsEnabled = true
                },
                Production = new MidtransEnvironmentOptions
                {
                    ServerKey = "production-server-key",
                    IsEnabled = true
                }
            });

            var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
            var httpClientFactory = new StubHttpClientFactory(httpClient);

            return new MidtransTransactionReconciliationService(
                dbContext,
                options,
                httpClientFactory,
                NullLogger<MidtransTransactionReconciliationService>.Instance);
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