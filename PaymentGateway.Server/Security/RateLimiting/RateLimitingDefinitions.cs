using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Server.Security.RateLimiting
{
    public static class RateLimitPolicyNames
    {
        public const string AuthLoginStrict = "auth_login_strict";
        public const string AuthRefreshModerate = "auth_refresh_moderate";
        public const string AuthLogoutModerate = "auth_logout_moderate";
        public const string SnapPublicModerate = "snap_public_moderate";
        public const string SnapStatusModerate = "snap_status_moderate";
        public const string SnapCancelModerate = "snap_cancel_moderate";
        public const string WebhookTolerant = "webhook_tolerant";
        public const string CallbackLenient = "callback_lenient";
    }

    public sealed class RateLimitSettings
    {
        public RateLimitPolicy AuthLoginStrict { get; set; } = new(5, 1, 60);
        public RateLimitPolicy AuthRefreshModerate { get; set; } = new(12, 2, 60);
        public RateLimitPolicy AuthLogoutModerate { get; set; } = new(20, 4, 60);
        public RateLimitPolicy SnapPublicModerate { get; set; } = new(20, 4, 60);
        public RateLimitPolicy SnapStatusModerate { get; set; } = new(30, 6, 60);
        public RateLimitPolicy SnapCancelModerate { get; set; } = new(10, 2, 60);
        public RateLimitPolicy WebhookTolerant { get; set; } = new(120, 20, 60);
        public RateLimitPolicy CallbackLenient { get; set; } = new(120, 20, 60);
    }

    public sealed record RateLimitPolicy(int PermitLimit, int QueueLimit, int WindowSeconds);

    public static class RateLimitKeyBuilder
    {
        public static string Build(HttpContext context, bool includeApiKeyHash)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
            var method = context.Request.Method;

            if (!includeApiKeyHash)
            {
                return $"{method}:{path}:{ip}";
            }

            var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            var apiKeyHash = string.IsNullOrWhiteSpace(apiKey)
                ? "no-api-key"
                : ToShortSha256(apiKey);

            return $"{method}:{path}:{ip}:{apiKeyHash}";
        }

        private static string ToShortSha256(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash)[..12].ToLowerInvariant();
        }
    }
}
