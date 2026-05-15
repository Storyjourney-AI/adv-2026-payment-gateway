using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_Login
    {
        public string Email { get; set; }
        public string Token { get; set; }
        
        /// <summary>
        /// Refresh token is ignored from JSON serialization and served to the client 
        /// via a secure HttpOnly cookie instead to prevent XSS attacks.
        /// The backend will set this as a cookie in the response automatically.
        /// </summary>
        [JsonIgnore]
        public string RefreshToken { get; set; }
        
        public bool IsNewUser { get; set; } = false;
    }

    public class Dto_LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
