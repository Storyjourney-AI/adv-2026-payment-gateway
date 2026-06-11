namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_WebhookForwardStatus
    {
        public Guid SnapTransactionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public int? LastResponseCode { get; set; }
        public string? LastError { get; set; }
    }
}
