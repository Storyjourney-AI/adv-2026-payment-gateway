using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Applications.Models.Dtos
{
    public class Dto_EnvironmentRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "AllowedOrigins cannot exceed 2000 characters")]
        public string AllowedOrigins { get; set; } = "*";

        [Url(ErrorMessage = "WebhookUrl must be a valid URL")]
        [StringLength(500, ErrorMessage = "WebhookUrl cannot exceed 500 characters")]
        public string? WebhookUrl { get; set; }

        [Required(ErrorMessage = "SuccessResponseUrl is required")]
        [Url(ErrorMessage = "SuccessResponseUrl must be a valid URL")]
        [StringLength(500, ErrorMessage = "SuccessResponseUrl cannot exceed 500 characters")]
        public string SuccessResponseUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "FailureResponseUrl is required")]
        [Url(ErrorMessage = "FailureResponseUrl must be a valid URL")]
        [StringLength(500, ErrorMessage = "FailureResponseUrl cannot exceed 500 characters")]
        public string FailureResponseUrl { get; set; } = string.Empty;

        // Only honoured on Update — Create always forces IsSandbox = true server-side
        public bool? IsSandbox { get; set; }
    }
}
