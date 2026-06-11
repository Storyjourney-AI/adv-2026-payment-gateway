namespace PaymentGateway.Server.Midtrans.Models
{
    public sealed class WebhookForwardRetryOptions
    {
        /// <summary>How often the retry service polls for due rows (seconds). Default: 30.</summary>
        public int IntervalSeconds { get; set; } = 30;

        /// <summary>Base backoff delay used in the exponential formula: min(MaxBackoffSeconds, BaseBackoffSeconds * 2^attempt). Default: 30.</summary>
        public int BaseBackoffSeconds { get; set; } = 30;

        /// <summary>Maximum backoff ceiling in seconds. Default: 3600 (1 hour).</summary>
        public int MaxBackoffSeconds { get; set; } = 3600;

        /// <summary>Maximum delivery attempts before the row is marked Exhausted. Default: 8.</summary>
        public int MaxAttempts { get; set; } = 8;

        /// <summary>Maximum number of due rows processed per tick. Default: 50.</summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// How long (seconds) a row is considered "leased" by an InProgress worker before it can be reclaimed
        /// by the drainer. MUST exceed the "webhook-forward" HttpClient.Timeout (currently 10 s) to avoid
        /// double-delivery while a slow-but-alive POST is still in flight. Default: 300 (5 minutes).
        /// </summary>
        public int InProgressLeaseSeconds { get; set; } = 300;
    }
}
