using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_PasswordForgetRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Url]
        public string CallbackBaseUrl { get; set; }
    }

    public class Dto_PasswordResetRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Length(8, 36, ErrorMessage = "Password must be within 8-36 characters")]
        public string Password { get; set; }

        [Required]
        [Url]
        public string PasswordResetUrl { get; set; }
    }
}
