using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_PasswordChange
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required] 
        public string OldPassword { get; set; }
        [Required]
        [Length(8, 36, ErrorMessage = "New password must be between 8-36 characters")]
        public string NewPassword { get; set; }
        [Required]
        [Url]
        public string ResetPasswordUrl { get; set; }
    }
}
