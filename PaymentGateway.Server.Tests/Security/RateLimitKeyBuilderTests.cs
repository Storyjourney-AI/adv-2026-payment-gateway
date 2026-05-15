using Microsoft.AspNetCore.Http;
using PaymentGateway.Server.Security.RateLimiting;
using System.Net;

namespace PaymentGateway.Server.Tests.Security
{
    public class RateLimitKeyBuilderTests
    {
        [Fact]
        public void Build_WithoutApiKey_ReturnsMethodPathIpKey()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/api/auth/login";
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.10.10.10");

            var key = RateLimitKeyBuilder.Build(context, includeApiKeyHash: false);

            Assert.Equal("POST:/api/auth/login:10.10.10.10", key);
        }

        [Fact]
        public void Build_WithApiKey_IncludesHashedApiKey_NotRawValue()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Path = "/api/snap/token";
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.1");
            context.Request.Headers["X-Api-Key"] = "plain-api-key-value";

            var key = RateLimitKeyBuilder.Build(context, includeApiKeyHash: true);

            Assert.Contains("POST:/api/snap/token:203.0.113.1:", key);
            Assert.DoesNotContain("plain-api-key-value", key);
        }
    }
}
