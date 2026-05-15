using Microsoft.Extensions.Caching.Memory;
using PaymentGateway.Server.Security.Webhook;

namespace PaymentGateway.Server.Tests.Security
{
    public class WebhookReplayGuardTests
    {
        [Fact]
        public void TryAcquire_ReturnsTrueOnFirstCall_AndFalseForDuplicateWithinTtl()
        {
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var guard = new WebhookReplayGuard(memoryCache);
            var ttl = TimeSpan.FromMinutes(5);

            var first = guard.TryAcquire("k:order:tx:status", ttl);
            var second = guard.TryAcquire("k:order:tx:status", ttl);

            Assert.True(first);
            Assert.False(second);
        }
    }
}
