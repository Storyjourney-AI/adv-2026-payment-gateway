using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_ValidateEmail
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [Url]
        public string ValidateEmailUrl { get; set; }
    }

    public class Dto_ValidateEmailExecute
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Token { get; set; }
    }
}
