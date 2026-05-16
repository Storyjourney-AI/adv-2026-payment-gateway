using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Midtrans.Controllers;
using PaymentGateway.Server.Midtrans.Services;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class SnapControllerTests
    {
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
    }
}