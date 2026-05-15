using System.IdentityModel.Tokens.Jwt;
using PaymentGateway.Server.ActivityLog.Models.Dbs;
using PaymentGateway.Server.Databases;

namespace PaymentGateway.Server.ActivityLog.Services
{
    public class ActivityLogService
    {
        private readonly AppDbContext m_dbContext;
        private readonly IHttpContextAccessor m_httpContextAccessor;
        private readonly ILogger<ActivityLogService> m_logger;

        public ActivityLogService(
            AppDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogService> logger)
        {
            m_dbContext = dbContext;
            m_httpContextAccessor = httpContextAccessor;
            m_logger = logger;
        }

        /// <summary>
        /// Log an activity with automatic session token extraction from the current HTTP context
        /// </summary>
        public async Task LogAsync(string category, string action, Guid? userId = null, string? userEmail = null, string? sessionToken = null)
        {
            try
            {
                // Extract session token from Authorization header if not provided
                if (string.IsNullOrEmpty(sessionToken))
                {
                    sessionToken = ExtractSessionToken();
                }

                // Extract user info from claims if not provided
                if (userId == null || string.IsNullOrEmpty(userEmail))
                {
                    var httpContext = m_httpContextAccessor.HttpContext;
                    if (httpContext?.User != null)
                    {
                        var userIdClaim = httpContext.User.FindFirst("sub_id")?.Value;
                        if (userId == null && !string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
                        {
                            userId = parsedUserId;
                        }

                        if (string.IsNullOrEmpty(userEmail))
                        {
                            userEmail = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                        }
                    }
                }

                var log = new Db_ActivityLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UserEmail = userEmail,
                    SessionToken = sessionToken,
                    Category = category,
                    Action = action,
                    Timestamp = DateTime.UtcNow
                };

                m_dbContext.ActivityLogs.Add(log);
                await m_dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Failed to write activity log: {Category} - {Action}", category, action);
            }
        }

        /// <summary>
        /// Extract the first 8 characters of the JWT jti claim (unique per token) from the Authorization header.
        /// The jti claim is a GUID generated per token, making it a reliable session identifier.
        /// </summary>
        private string? ExtractSessionToken()
        {
            var httpContext = m_httpContextAccessor.HttpContext;
            var authHeader = httpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwt = handler.ReadJwtToken(token);
                        var jti = jwt.Id; // The jti claim
                        if (!string.IsNullOrEmpty(jti) && jti.Length >= 8)
                        {
                            return jti.Substring(0, 8);
                        }
                    }
                }
                catch
                {
                    // Fall back: if token can't be parsed, return null
                }
            }
            return null;
        }

        /// <summary>
        /// Extract session token from a raw JWT string (for login where token was just created)
        /// </summary>
        public static string? ExtractSessionTokenFromJwt(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    var jti = jwt.Id;
                    if (!string.IsNullOrEmpty(jti) && jti.Length >= 8)
                    {
                        return jti.Substring(0, 8);
                    }
                }
            }
            catch
            {
                // Token couldn't be parsed
            }
            return null;
        }
    }

    /// <summary>
    /// Constants for activity log categories
    /// </summary>
    public static class ActivityLogCategory
    {
        public const string Login = "login";
        public const string Creation = "creation";
        public const string Modification = "modification";
        public const string Deletion = "deletion";
    }
}
