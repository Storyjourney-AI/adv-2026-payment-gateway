using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_Register
    {
        // for internal logic reference
        [JsonIgnore] 
        public string? UserId { get;set; }

        // - jsonized item
        public string Email { get; set; }
        public DateTime RegisteredOn { get; set; }
        
        /// <summary>
        /// Indicates if this is the first user (Super Admin). Only set for initial registration, null otherwise.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsInitialUser { get; set; }
    }

    public class Dto_RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "Password must be within 8-100 characters")]
        [MaxLength(100, ErrorMessage = "Password must be within 8-100 characters")]
        public string Password { get; set; }

        [Required]
        [Compare("Password", ErrorMessage = "The password and confirmation password does not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the terms.")]
        public bool Agreement { get; set; }
    }
}
