using System.Text.Json.Serialization;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    /// <summary>
    /// Refresh token request DTO
    /// Token comes from HttpOnly cookie, not from request body
    /// </summary>
    public class Dto_RefreshTokenRequest
    {
        /// <summary>
        /// Optional refresh token property for API testing purposes
        /// In production, token is read from HttpOnly cookie: Request.Cookies["refreshToken"]
        /// </summary>
        [JsonIgnore]
        public string? RefreshToken { get; set; }
    }

    /// <summary>
    /// Refresh token response DTO
    /// Returns new access token and refresh token after successful refresh
    /// </summary>
    public class Dto_RefreshTokenResponse
    {
        /// <summary>
        /// New access token (JWT)
        /// Sent in response body, stored in client memory
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// New refresh token (rotated)
        /// Ignored from JSON serialization and served via HttpOnly cookie instead
        /// </summary>
        [JsonIgnore]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
