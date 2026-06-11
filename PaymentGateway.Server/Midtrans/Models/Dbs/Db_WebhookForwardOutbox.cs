namespace PaymentGateway.Server.Midtrans.Models.Dbs
{
    public static class WebhookForwardStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Delivered = "Delivered";
        public const string Failed = "Failed";
        public const string Exhausted = "Exhausted";
    }

    public class Db_WebhookForwardOutbox
    {
        public Guid Id { get; set; }
        public Guid EnvironmentId { get; set; }

        /// <summary>FK to Db_SnapTransaction. Unique: one active outbox row per transaction (upserted).</summary>
        public Guid SnapTransactionId { get; set; }

        public string MidtransOrderId { get; set; } = string.Empty;
        public string CallerOrderId { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;

        /// <summary>The serialised forward payload (enriched Midtrans body with gateway_fee_breakdown).</summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// The original raw Midtrans notification body received at enqueue time.
        /// Used for payload-faithful retries so that all fields (signature_key, transaction_time, etc.) are preserved.
        /// </summary>
        public string RawNotificationBody { get; set; } = string.Empty;

        /// <summary>One of <see cref="WebhookForwardStatus"/> constants: Pending / InProgress / Delivered / Failed / Exhausted.</summary>
        public string Status { get; set; } = WebhookForwardStatus.Pending;

        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }

        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }

        public int? LastResponseCode { get; set; }
        public string? LastError { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Db_SnapTransaction? Transaction { get; set; }
    }
}
