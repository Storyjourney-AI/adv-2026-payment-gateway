using PaymentGateway.Server.Common.Interfaces;

namespace PaymentGateway.Server.Applications.Models.Dbs
{
    public class Db_Environment : ISoftDelete
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string AllowedOrigins { get; set; } = "*";
        public string? WebhookUrl { get; set; }
        public string SuccessResponseUrl { get; set; } = string.Empty;
        public string? PendingResponseUrl { get; set; }
        public string FailureResponseUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsSandbox { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation property
        public Db_Application? Application { get; set; }
    }
}
