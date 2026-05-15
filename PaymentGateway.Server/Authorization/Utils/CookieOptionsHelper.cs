using Microsoft.AspNetCore.Http;

namespace PaymentGateway.Server.Authorization.Utils
{
    /// <summary>
    /// Helper class to manage consistent HttpOnly cookie options across the application
    /// </summary>
    public static class CookieOptionsHelper
    {
        private static CookieOptions _refreshTokenOptions;

        /// <summary>
        /// Initialize cookie options based on the current environment
        /// Should be called once during application startup in Program.cs
        /// </summary>
        public static void Initialize(IWebHostEnvironment environment)
        {
            _refreshTokenOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment(), // HTTPS in production, HTTP in development
                SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };
        }

        /// <summary>
        /// Get the refresh token cookie options
        /// </summary>
        public static CookieOptions GetRefreshTokenCookieOptions()
        {
            if (_refreshTokenOptions == null)
            {
                throw new InvalidOperationException("CookieOptionsHelper has not been initialized. Call Initialize() in Program.cs");
            }

            return _refreshTokenOptions;
        }

        /// <summary>
        /// Get cookie options for deleting the refresh token (same options as creation)
        /// </summary>
        public static CookieOptions GetRefreshTokenDeleteOptions()
        {
            if (_refreshTokenOptions == null)
            {
                throw new InvalidOperationException("CookieOptionsHelper has not been initialized. Call Initialize() in Program.cs");
            }

            return _refreshTokenOptions;
        }
    }
}
