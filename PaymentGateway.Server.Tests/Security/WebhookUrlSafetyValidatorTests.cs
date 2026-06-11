using PaymentGateway.Server.Security.Webhook;

namespace PaymentGateway.Server.Tests.Security
{
    /// <summary>
    /// Tests for <see cref="WebhookUrlSafetyValidator"/>.
    /// Covers: https-only enforcement, private/reserved IP rejection, loopback rejection,
    /// and public-routable https URLs are allowed.
    ///
    /// Note: Tests that rely on DNS resolution (e.g. a hostname that resolves to a private IP)
    /// are not included here because they depend on the network environment and the validator
    /// performs a real DNS lookup. The public-hostname test uses a hard-coded IPv4 literal
    /// (8.8.8.8) to avoid network dependency.
    /// </summary>
    public class WebhookUrlSafetyValidatorTests
    {
        private static readonly WebhookUrlSafetyValidator Validator = new();

        // ── scheme enforcement ────────────────────────────────────────────────

        [Theory]
        [InlineData("http://example.com/webhook")]
        [InlineData("http://8.8.8.8/webhook")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForHttpScheme(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for HTTP URL: {url}");
        }

        [Theory]
        [InlineData("ftp://example.com/webhook")]
        [InlineData("ws://example.com/webhook")]
        [InlineData("file:///etc/passwd")]
        [InlineData("not-a-url")]
        [InlineData("")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForNonHttpsScheme(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for non-https URL: {url}");
        }

        // ── loopback ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData("https://localhost/webhook")]
        [InlineData("https://127.0.0.1/webhook")]
        [InlineData("https://127.0.0.2/webhook")]
        [InlineData("https://[::1]/webhook")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForLoopbackAddress(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for loopback URL: {url}");
        }

        // ── RFC-1918 private address ranges ───────────────────────────────────

        [Theory]
        [InlineData("https://10.0.0.1/webhook")]      // 10.0.0.0/8
        [InlineData("https://10.255.255.255/webhook")]
        [InlineData("https://172.16.0.1/webhook")]     // 172.16.0.0/12
        [InlineData("https://172.31.255.255/webhook")]
        [InlineData("https://192.168.1.1/webhook")]    // 192.168.0.0/16
        [InlineData("https://192.168.255.255/webhook")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForPrivateRfc1918Address(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for RFC-1918 private IP URL: {url}");
        }

        // ── link-local (169.254.0.0/16) ───────────────────────────────────────

        [Theory]
        [InlineData("https://169.254.0.1/webhook")]
        [InlineData("https://169.254.169.254/webhook")]  // AWS metadata endpoint
        [InlineData("https://169.254.255.255/webhook")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForLinkLocalAddress(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for link-local IP URL: {url}");
        }

        // ── carrier-grade NAT (100.64.0.0/10) ────────────────────────────────

        [Theory]
        [InlineData("https://100.64.0.1/webhook")]
        [InlineData("https://100.127.255.255/webhook")]
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForCarrierGradeNatAddress(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for carrier-grade NAT IP URL: {url}");
        }

        // ── reserved / multicast / unspecified ───────────────────────────────

        [Theory]
        [InlineData("https://0.0.0.0/webhook")]         // unspecified
        [InlineData("https://224.0.0.1/webhook")]        // multicast
        [InlineData("https://255.255.255.255/webhook")]  // broadcast / reserved
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForReservedOrMulticastAddress(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for reserved/multicast IP URL: {url}");
        }

        // ── IPv6 private ──────────────────────────────────────────────────────

        [Theory]
        [InlineData("https://[fc00::1]/webhook")]    // fc00::/7 unique local
        [InlineData("https://[fd00::1]/webhook")]    // fd00::/8 unique local
        [InlineData("https://[fe80::1]/webhook")]    // link-local
        public async Task IsWebhookUrlSafeAsync_ReturnsFalse_ForPrivateIpv6Address(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.False(result, $"Expected SSRF rejection for private IPv6 URL: {url}");
        }

        // ── public https IPs (safe) ───────────────────────────────────────────

        [Theory]
        [InlineData("https://8.8.8.8/webhook")]        // Google DNS — public
        [InlineData("https://1.1.1.1/webhook")]        // Cloudflare DNS — public
        [InlineData("https://8.8.8.8/some/path?q=1")] // path + query string
        public async Task IsWebhookUrlSafeAsync_ReturnsTrue_ForPublicHttpsIpUrl(string url)
        {
            var result = await Validator.IsWebhookUrlSafeAsync(url);
            Assert.True(result, $"Expected safe result for public HTTPS IP URL: {url}");
        }
    }
}
