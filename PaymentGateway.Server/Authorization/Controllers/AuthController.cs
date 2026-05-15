using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PaymentGateway.Server.ActivityLog.Services;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Authorization.Services;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Security.Captcha;
using PaymentGateway.Server.Security.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PaymentGateway.Server.Authorization.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService m_authService;
        private readonly ActivityLogService m_activityLog;
        private readonly ILogger<AuthController> m_logger;
        private readonly IConfiguration m_config;
        private readonly ITurnstileValidationService m_turnstileValidationService;

        public AuthController(
            AuthService authService,
            ActivityLogService activityLog,
            ILogger<AuthController> logger,
            IConfiguration config,
            ITurnstileValidationService turnstileValidationService)
        {
            m_authService = authService;
            m_activityLog = activityLog;
            m_logger = logger;
            m_config = config;
            m_turnstileValidationService = turnstileValidationService;
        }

        /// <summary>
        /// Register a new user account
        /// DISABLED: Registration is temporarily disabled. Contact administrator for account creation.
        /// </summary>
        /// <param name="registerRequest">Registration data including email, password, and optional invitation code</param>
        /// <returns>Registration result with user details</returns>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLoginStrict)]
        [Obsolete("Registration endpoint is disabled")]
        public async Task<ActionResult<DataWrapper<Dto_Register>>> Register([FromBody] Dto_RegisterRequest registerRequest)
        {
            // Registration disabled - return 404
            return NotFound(DataWrapper<Dto_Register>.NotFound(
                message: "Registration is currently disabled. Please contact the administrator for account creation."));

            /* COMMENTED OUT - Registration implementation kept for future reference
            try
            {
                m_logger.LogInformation("Register endpoint called for email: {email}", registerRequest.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("Register - Validation failed for email: {email}. Errors: {errors}", 
                        registerRequest.Email, string.Join(", ", errors));
                    
                    return BadRequest(DataWrapper<Dto_Register>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var result = await m_authService.RegisterAsync(registerRequest);

                m_logger.LogInformation("Register endpoint result - Success: {success}, Code: {code}", 
                    result.Success, result.Code);

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Register endpoint - Unexpected error for email: {email}", 
                    registerRequest?.Email ?? "unknown");
                
                return StatusCode(500, DataWrapper<Dto_Register>.Fail_InternalError(
                    message: "An unexpected error occurred during registration. Please try again later."));
            }
            */
        }

        /// <summary>
        /// Login with email and password
        /// </summary>
        /// <param name="loginRequest">Login credentials including email and password</param>
        /// <returns>Login result with JWT token</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLoginStrict)]
        public async Task<ActionResult<DataWrapper<Dto_Login>>> Login([FromBody] Dto_LoginRequest loginRequest)
        {
            try
            {
                var captchaCheck = await ValidateCaptchaAsync();
                if (captchaCheck != null)
                {
                    return captchaCheck;
                }

                m_logger.LogInformation("Login endpoint called for email: {email}", loginRequest.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("Login - Validation failed for email: {email}. Errors: {errors}", 
                        loginRequest.Email, string.Join(", ", errors));
                    
                    return BadRequest(DataWrapper<Dto_Login>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var result = await m_authService.LoginAsync(loginRequest);

                m_logger.LogInformation("Login endpoint result - Success: {success}, Code: {code}", 
                    result.Success, result.Code);

                // If login successful, set refresh token as HttpOnly cookie
                if (result.Success && result.Data?.RefreshToken != null)
                {
                    Response.Cookies.Append("refreshToken", result.Data.RefreshToken, CookieOptionsHelper.GetRefreshTokenCookieOptions());
                    
                    m_logger.LogInformation("Refresh token set as HttpOnly cookie for user: {email}", loginRequest.Email);
                    
                    // Clear the refresh token from response data (already in cookie)
                    result.Data.RefreshToken = null;
                }

                // Log successful login activity
                if (result.Success && result.Data != null)
                {
                    var sessionToken = ActivityLogService.ExtractSessionTokenFromJwt(result.Data.Token);
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Login,
                        $"User logged in: {loginRequest.Email}",
                        userId: null,
                        userEmail: loginRequest.Email,
                        sessionToken: sessionToken);
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Login endpoint - Unexpected error for email: {email}", 
                    loginRequest?.Email ?? "unknown");
                
                return StatusCode(500, DataWrapper<Dto_Login>.Fail_InternalError(
                    message: "An unexpected error occurred during login. Please try again later."));
            }
        }

        /// <summary>
        /// Validate the provided JWT token
        /// </summary>
        /// <returns>Validation result with token claims information</returns>
        [HttpPost("validate")]
        [Authorize]
        public ActionResult<DataWrapper<Dto_TokenValidation>> Validate()
        {
            try
            {
                m_logger.LogInformation("Validate token endpoint called");

                var userId = User.FindFirst("sub_id")?.Value ?? 
                            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                            string.Empty;

                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? 
                           User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? 
                           string.Empty;

                var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

                var issuedAtStr = User.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
                var expiresAtStr = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

                var issuedAt = !string.IsNullOrEmpty(issuedAtStr) && long.TryParse(issuedAtStr, out var iatUnix)
                    ? UnixTimeStampToDateTime(iatUnix)
                    : (DateTime?)null;

                var expiresAt = !string.IsNullOrEmpty(expiresAtStr) && long.TryParse(expiresAtStr, out var expUnix)
                    ? UnixTimeStampToDateTime(expUnix)
                    : DateTime.MinValue;

                var tokenData = new Dto_TokenValidation
                {
                    UserId = userId,
                    Email = email,
                    Roles = roles,
                    ExpiresAt = expiresAt,
                    IssuedAt = issuedAt,
                    IsValid = true
                };

                m_logger.LogInformation("Validate token - Token validation successful for user: {userId}", userId);

                return Ok(DataWrapper<Dto_TokenValidation>.Succeed(
                    tokenData,
                    message: "Token is valid."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Validate token endpoint - Unexpected error");
                
                return StatusCode(500, DataWrapper<Dto_TokenValidation>.Fail_InternalError(
                    message: "An unexpected error occurred during token validation."));
            }
        }

        /// <summary>
        /// Refresh the access token using the refresh token from HttpOnly cookie
        /// </summary>
        /// <returns>New access token with refreshed refresh token in HttpOnly cookie</returns>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.AuthRefreshModerate)]
        public async Task<ActionResult<DataWrapper<Dto_RefreshTokenResponse>>> Refresh()
        {
            try
            {
                m_logger.LogInformation("Refresh token endpoint called");

                // Read refresh token from HttpOnly cookie
                if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    m_logger.LogWarning("Refresh endpoint - No refresh token found in cookies");
                    return Unauthorized(DataWrapper<Dto_RefreshTokenResponse>.Unauthorized(
                        message: "Refresh token is missing or invalid."));
                }

                // Call service to refresh tokens
                var result = await m_authService.RefreshAccessTokenAsync(refreshToken);

                if (!result.Success)
                {
                    m_logger.LogWarning("Refresh endpoint - Token refresh failed: {message}", result.Message);
                    
                    // Clear invalid refresh token cookie
                    Response.Cookies.Delete("refreshToken", CookieOptionsHelper.GetRefreshTokenDeleteOptions());
                    
                    return StatusCode((int)result.Code, result);
                }

                // If successful, set new refresh token in HttpOnly cookie
                if (result.Data?.RefreshToken != null)
                {
                    Response.Cookies.Append("refreshToken", result.Data.RefreshToken, CookieOptionsHelper.GetRefreshTokenCookieOptions());
                    
                    m_logger.LogInformation("Refresh endpoint - New refresh token set as HttpOnly cookie");
                    
                    // Clear the refresh token from response data (already in cookie)
                    result.Data.RefreshToken = null;
                }

                m_logger.LogInformation("Refresh endpoint - Token refresh successful");
                return Ok(result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Refresh endpoint - Unexpected error");
                
                return StatusCode(500, DataWrapper<Dto_RefreshTokenResponse>.Fail_InternalError(
                    message: "An unexpected error occurred during token refresh. Please try again later."));
            }
        }

        /// <summary>
        /// Logout user by invalidating their refresh token
        /// </summary>
        /// <returns>Logout result</returns>
        [HttpPost("logout")]
        [AllowAnonymous] // Allow unauthenticated access since we read refresh token from cookie instead
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogoutModerate)]
        public async Task<ActionResult<DataWrapper<bool>>> Logout()
        {
            try
            {
                m_logger.LogInformation("Logout endpoint called");

                // Read refresh token from HttpOnly cookie
                if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    m_logger.LogWarning("Logout endpoint - No refresh token found in cookies");
                    
                    // Still clear the cookie and return success even if no token found
                    Response.Cookies.Delete("refreshToken", CookieOptionsHelper.GetRefreshTokenDeleteOptions());
                    
                    return Ok(DataWrapper<bool>.Succeed(true, message: "Logout successful."));
                }

                // Call service to logout (delete refresh token)
                var result = await m_authService.LogoutAsync(refreshToken);

                if (!result.Success)
                {
                    m_logger.LogWarning("Logout endpoint - Logout failed: {message}", result.Message);
                }

                // Clear refresh token cookie with matching options
                Response.Cookies.Delete("refreshToken", CookieOptionsHelper.GetRefreshTokenDeleteOptions());
                
                m_logger.LogInformation("Logout endpoint - Logout successful, refresh token cookie cleared");

                // Log logout activity
                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Login,
                        "User logged out");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Logout endpoint - Unexpected error");
                
                // Still try to clear the cookie with matching options
                Response.Cookies.Delete("refreshToken", CookieOptionsHelper.GetRefreshTokenDeleteOptions());
                
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred during logout. Please try again later."));
            }
        }

        /// <summary>
        /// Change user password
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<DataWrapper<bool>>> ChangePassword([FromBody] Dto_ChangePasswordRequest request)
        {
            try
            {
                // Resolve caller identity from JWT
                var callerId = User.FindFirst("sub_id")?.Value;
                if (string.IsNullOrEmpty(callerId))
                {
                    return Unauthorized(DataWrapper<bool>.Unauthorized(
                        message: "User identity could not be resolved."));
                }

                var callerUser = await m_authService.GetUserByIdAsync(callerId);
                if (callerUser == null)
                {
                    return Unauthorized(DataWrapper<bool>.Unauthorized(
                        message: "User not found."));
                }

                // Verify the email in the request matches the authenticated user
                if (!string.Equals(callerUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                {
                    m_logger.LogWarning("Change password - Email mismatch for caller {CallerId}: requested '{RequestEmail}'", callerId, request.Email);
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Email does not match the authenticated account."));
                }

                m_logger.LogInformation("Change password endpoint called for email: {email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("Change password - Validation failed for email: {email}. Errors: {errors}",
                        request.Email, string.Join(", ", errors));

                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var user = callerUser;

                // Verify current password
                var validPassword = await m_authService.ValidatePasswordAsync(user, request.CurrentPassword);
                if (!validPassword)
                {
                    m_logger.LogWarning("Change password - Invalid current password for email: {email}", request.Email);
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Current password is incorrect"));
                }

                // Change password
                var result = await m_authService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

                m_logger.LogInformation("Change password endpoint result - Success: {success}, Code: {code}",
                    result.Success, result.Code);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"User changed password: {request.Email}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Change password endpoint - Unexpected error for email: {email}",
                    request?.Email ?? "unknown");

                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while changing password. Please try again later."));
            }
        }

        /// <summary>
        /// Convert Unix timestamp to DateTime
        /// </summary>
        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dateTime;
        }

        private async Task<ActionResult?> ValidateCaptchaAsync()
        {
            var validation = await m_turnstileValidationService.ValidateRequestAsync(HttpContext, HttpContext.RequestAborted);
            if (validation.Success)
            {
                return null;
            }

            m_logger.LogWarning("Captcha validation rejected request for path {Path}", Request.Path);
            return Unauthorized(DataWrapper<object>.Unauthorized(message: validation.Message));
        }
    }
}
