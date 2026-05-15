namespace PaymentGateway.Server.Security.Webhook
{
    public sealed class WebhookHardeningOptions
    {
        public int ReplayWindowMinutes { get; set; } = 15;
        public int DeduplicationWindowMinutes { get; set; } = 60;
        public bool RejectWhenTransactionTimeMissing { get; set; } = false;
        public int ForwardRetryCount { get; set; } = 1;
        public int ForwardRetryDelayMs { get; set; } = 300;
    }
}
