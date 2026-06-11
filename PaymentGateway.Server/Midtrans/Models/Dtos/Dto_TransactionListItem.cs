namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_TransactionListItem
    {
        public Guid Id { get; set; }
        public string CallerOrderId { get; set; } = string.Empty;
        public string MidtransOrderId { get; set; } = string.Empty;
        public int GrossAmount { get; set; }
        public string? TransactionStatus { get; set; }
        public string MidtransEnv { get; set; } = string.Empty;
        public string? MidtransTransactionId { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public bool IsSandbox { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Webhook forward outbox fields — null when no outbox row exists for the transaction
        public string? WebhookForwardStatus { get; set; }
        public int? WebhookAttemptCount { get; set; }
        public DateTime? WebhookLastAttemptAt { get; set; }
        public int? WebhookLastResponseCode { get; set; }
    }
}
