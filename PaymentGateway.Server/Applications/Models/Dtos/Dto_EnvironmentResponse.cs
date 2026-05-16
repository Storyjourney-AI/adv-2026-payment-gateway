namespace PaymentGateway.Server.Applications.Models.Dtos
{
    public class Dto_EnvironmentResponse
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string AllowedOrigins { get; set; } = "*";
        public string? WebhookUrl { get; set; }
        public string SuccessResponseUrl { get; set; } = string.Empty;
        public string PendingResponseUrl { get; set; } = string.Empty;
        public string FailureResponseUrl { get; set; } = string.Empty;
        public bool IsSandbox { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
