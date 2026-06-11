using System.Net;
using System.Net.Sockets;

namespace PaymentGateway.Server.Security.Webhook
{
    public interface IWebhookUrlSafetyValidator
    {
        /// <summary>
        /// Returns true if <paramref name="url"/> is a safe webhook target:
        /// HTTPS scheme, non-loopback, and resolves only to public-routable IP addresses.
        /// </summary>
        Task<bool> IsWebhookUrlSafeAsync(string url);
    }

    public sealed class WebhookUrlSafetyValidator : IWebhookUrlSafetyValidator
    {
        public async Task<bool> IsWebhookUrlSafeAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return false;

            if (uri.IsLoopback)
                return false;

            if (IPAddress.TryParse(uri.Host, out var directIp))
            {
                return !IsPrivateOrReservedIp(directIp);
            }

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
                if (addresses.Length == 0)
                {
                    return false;
                }

                return addresses.All(ip => !IsPrivateOrReservedIp(ip));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPrivateOrReservedIp(IPAddress ipAddress)
        {
            if (IPAddress.IsLoopback(ipAddress))
            {
                return true;
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6Multicast || ipAddress.IsIPv6SiteLocal)
                {
                    return true;
                }

                var bytes = ipAddress.GetAddressBytes();
                // fc00::/7 unique local address
                return (bytes[0] & 0xFE) == 0xFC;
            }

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytesV4 = ipAddress.GetAddressBytes();
            var first = bytesV4[0];
            var second = bytesV4[1];

            // 10.0.0.0/8
            if (first == 10) return true;
            // 127.0.0.0/8
            if (first == 127) return true;
            // 169.254.0.0/16
            if (first == 169 && second == 254) return true;
            // 172.16.0.0/12
            if (first == 172 && second >= 16 && second <= 31) return true;
            // 192.168.0.0/16
            if (first == 192 && second == 168) return true;
            // 100.64.0.0/10 carrier-grade NAT
            if (first == 100 && second >= 64 && second <= 127) return true;
            // 0.0.0.0/8 and multicast/reserved 224.0.0.0+
            if (first == 0 || first >= 224) return true;

            return false;
        }
    }
}
