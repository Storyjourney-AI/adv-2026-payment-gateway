using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Security.Captcha;
using PaymentGateway.Server.Security.Operations;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PaymentGateway.Server.Tests.Security
{
    public class TurnstileValidationServiceTests
    {
        [Fact]
        public async Task ValidateRequestAsync_ReturnsSuccess_WhenFeatureDisabled()
        {
            var service = CreateService(
                new TurnstileOptions { IsEnabled = false },
                "{}",
                "Production");

            var context = new DefaultHttpContext();
            var result = await service.ValidateRequestAsync(context);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task ValidateRequestAsync_ReturnsFailure_WhenHeaderMissing()
        {
            var service = CreateService(
                new TurnstileOptions { IsEnabled = true, SecretKey = "secret", HeaderName = "X-Turnstile-Token" },
                "{\"success\":true}",
                "Production");

            var context = new DefaultHttpContext();
            var result = await service.ValidateRequestAsync(context);

            Assert.False(result.Success);
            Assert.Contains("header is required", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateRequestAsync_ReturnsSuccess_WhenDevelopmentBypassTokenMatches()
        {
            var service = CreateService(
                new TurnstileOptions
                {
                    IsEnabled = true,
                    SecretKey = "secret",
                    HeaderName = "X-Turnstile-Token",
                    AllowBypassInDevelopment = true,
                    DevelopmentBypassToken = "dev-turnstile-bypass"
                },
                "{\"success\":false}",
                "Development");

            var context = new DefaultHttpContext();
            context.Request.Headers["X-Turnstile-Token"] = "dev-turnstile-bypass";

            var result = await service.ValidateRequestAsync(context);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task ValidateRequestAsync_ReturnsFailure_WhenProviderRejectsToken()
        {
            var service = CreateService(
                new TurnstileOptions
                {
                    IsEnabled = true,
                    SecretKey = "secret",
                    HeaderName = "X-Turnstile-Token"
                },
                "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}",
                "Production");

            var context = new DefaultHttpContext();
            context.Request.Headers["X-Turnstile-Token"] = "bad-token";

            var result = await service.ValidateRequestAsync(context);

            Assert.False(result.Success);
            Assert.Equal("Captcha verification failed.", result.Message);
        }

        private static TurnstileValidationService CreateService(
            TurnstileOptions options,
            string responseJson,
            string environmentName)
        {
            var handler = new StaticJsonMessageHandler(responseJson);
            var httpClient = new HttpClient(handler);
            var factory = new StubHttpClientFactory(httpClient);
            var hostEnvironment = new StubHostEnvironment(environmentName);

            return new TurnstileValidationService(
                factory,
                Options.Create(options),
                hostEnvironment,
                new SecurityMetricsService(),
                NullLogger<TurnstileValidationService>.Instance);
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient m_client;

            public StubHttpClientFactory(HttpClient client)
            {
                m_client = client;
            }

            public HttpClient CreateClient(string name) => m_client;
        }

        private sealed class StaticJsonMessageHandler : HttpMessageHandler
        {
            private readonly string m_json;

            public StaticJsonMessageHandler(string json)
            {
                m_json = json;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(m_json, Encoding.UTF8, "application/json")
                };

                return Task.FromResult(response);
            }
        }

        private sealed class StubHostEnvironment : IHostEnvironment
        {
            public StubHostEnvironment(string environmentName)
            {
                EnvironmentName = environmentName;
                ApplicationName = "tests";
                ContentRootPath = AppContext.BaseDirectory;
                ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
            }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }
    }
}
