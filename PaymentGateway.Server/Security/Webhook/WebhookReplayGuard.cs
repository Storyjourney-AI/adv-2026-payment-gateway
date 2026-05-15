using Microsoft.Extensions.Caching.Memory;

namespace PaymentGateway.Server.Security.Webhook
{
    public sealed class WebhookReplayGuard : IWebhookReplayGuard
    {
        private readonly IMemoryCache m_cache;

        public WebhookReplayGuard(IMemoryCache cache)
        {
            m_cache = cache;
        }

        public bool TryAcquire(string dedupeKey, TimeSpan ttl)
        {
            if (m_cache.TryGetValue(dedupeKey, out _))
            {
                return false;
            }

            m_cache.Set(dedupeKey, true, ttl);
            return true;
        }
    }
}
