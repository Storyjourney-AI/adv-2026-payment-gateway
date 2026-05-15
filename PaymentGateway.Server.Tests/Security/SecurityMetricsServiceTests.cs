using PaymentGateway.Server.Security.Operations;

namespace PaymentGateway.Server.Tests.Security
{
    public class SecurityMetricsServiceTests
    {
        [Fact]
        public void Increment_AddsAndAggregatesMetricCounts()
        {
            var service = new SecurityMetricsService();

            service.Increment("rate_limit_reject_total", "/api/auth/login");
            service.Increment("rate_limit_reject_total", "/api/auth/login");
            service.Increment("captcha_validation_fail_total", "/api/auth/login");

            var snapshots = service.GetSnapshots();
            var rateLimit = snapshots.Single(x =>
                x.MetricName == "rate_limit_reject_total" &&
                x.Dimension == "/api/auth/login");
            var captcha = snapshots.Single(x =>
                x.MetricName == "captcha_validation_fail_total" &&
                x.Dimension == "/api/auth/login");

            Assert.Equal(2, rateLimit.Count);
            Assert.Equal(1, captcha.Count);
        }
    }
}
