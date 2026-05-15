namespace PaymentGateway.Server.Security.Webhook
{
    public interface IWebhookReplayGuard
    {
        bool TryAcquire(string dedupeKey, TimeSpan ttl);
    }
}
