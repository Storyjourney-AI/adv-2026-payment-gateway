using Microsoft.Extensions.Options;
using PaymentGateway.Server.Security.Operations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateway.Server.Security.Captcha
{
    public sealed class TurnstileValidationService : ITurnstileValidationService
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly IOptions<TurnstileOptions> m_options;
        private readonly IHostEnvironment m_hostEnvironment;
        private readonly ISecurityMetricsService m_securityMetricsService;
        private readonly ILogger<TurnstileValidationService> m_logger;

        public TurnstileValidationService(
            IHttpClientFactory httpClientFactory,
            IOptions<TurnstileOptions> options,
            IHostEnvironment hostEnvironment,
            ISecurityMetricsService securityMetricsService,
            ILogger<TurnstileValidationService> logger)
        {
            m_httpClientFactory = httpClientFactory;
            m_options = options;
            m_hostEnvironment = hostEnvironment;
            m_securityMetricsService = securityMetricsService;
            m_logger = logger;
        }

        public async Task<TurnstileValidationResult> ValidateRequestAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            var options = m_options.Value;
            if (!options.IsEnabled)
            {
                return TurnstileValidationResult.Ok();
            }

            var token = httpContext.Request.Headers[options.HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(token))
            {
                m_securityMetricsService.Increment("captcha_validation_fail_total", httpContext.Request.Path.Value ?? "unknown_path");
                return TurnstileValidationResult.Fail($"{options.HeaderName} header is required.");
            }

            if (m_hostEnvironment.IsDevelopment() &&
                options.AllowBypassInDevelopment &&
                string.Equals(token, options.DevelopmentBypassToken, StringComparison.Ordinal))
            {
                return TurnstileValidationResult.Ok();
            }

            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                m_logger.LogError("Turnstile validation failed because SecretKey is not configured.");
                m_securityMetricsService.Increment("captcha_validation_fail_total", httpContext.Request.Path.Value ?? "unknown_path");
                return TurnstileValidationResult.Fail("Captcha service is not configured.");
            }

            try
            {
                var payload = new Dictionary<string, string>
                {
                    ["secret"] = options.SecretKey,
                    ["response"] = token
                };

                var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
                if (!string.IsNullOrWhiteSpace(remoteIp))
                {
                    payload["remoteip"] = remoteIp;
                }

                var client = m_httpClientFactory.CreateClient("turnstile-verify");
                using var response = await client.PostAsync(
                    options.VerificationUrl,
                    new FormUrlEncodedContent(payload),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    m_logger.LogWarning(
                        "Turnstile verification endpoint returned non-success status {StatusCode}",
                        response.StatusCode);
                    m_securityMetricsService.Increment("captcha_validation_fail_total", httpContext.Request.Path.Value ?? "unknown_path");
                    return TurnstileValidationResult.Fail("Captcha verification failed.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var verification = await JsonSerializer.DeserializeAsync<TurnstileVerifyResponse>(
                    stream,
                    s_jsonOptions,
                    cancellationToken);

                if (verification?.Success == true)
                {
                    return TurnstileValidationResult.Ok();
                }

                var errors = verification?.ErrorCodes is { Length: > 0 }
                    ? string.Join(",", verification.ErrorCodes)
                    : "unknown";

                m_logger.LogWarning("Turnstile verification rejected token. Error codes: {ErrorCodes}", errors);
                m_securityMetricsService.Increment("captcha_validation_fail_total", httpContext.Request.Path.Value ?? "unknown_path");
                return TurnstileValidationResult.Fail("Captcha verification failed.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Unexpected error during Turnstile verification.");
                m_securityMetricsService.Increment("captcha_validation_fail_total", httpContext.Request.Path.Value ?? "unknown_path");
                return TurnstileValidationResult.Fail("Captcha verification failed.");
            }
        }

        private sealed class TurnstileVerifyResponse
        {
            public bool Success { get; set; }
            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
