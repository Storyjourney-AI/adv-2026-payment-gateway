using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Applications.Models.Dtos
{
    public class Dto_ApplicationRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 200 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }
    }
}
