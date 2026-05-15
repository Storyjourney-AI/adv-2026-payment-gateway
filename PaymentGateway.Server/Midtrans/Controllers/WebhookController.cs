using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Utils;
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.RateLimiting;
using PaymentGateway.Server.Security.Webhook;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Midtrans.Controllers
{
    [ApiController]
    [Route("api/midtrans")]
    [AllowAnonymous]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext m_dbContext;
        private readonly MidtransOptions m_midtransOptions;
        private readonly WebhookHardeningOptions m_webhookHardeningOptions;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly IWebhookReplayGuard m_webhookReplayGuard;
        private readonly ISecurityMetricsService m_securityMetricsService;
        private readonly ILogger<WebhookController> m_logger;

        public WebhookController(
            AppDbContext dbContext,
            IOptions<MidtransOptions> midtransOptions,
            IOptions<WebhookHardeningOptions> webhookHardeningOptions,
            IHttpClientFactory httpClientFactory,
            IWebhookReplayGuard webhookReplayGuard,
            ISecurityMetricsService securityMetricsService,
            ILogger<WebhookController> logger)
        {
            m_dbContext = dbContext;
            m_midtransOptions = midtransOptions.Value;
            m_webhookHardeningOptions = webhookHardeningOptions.Value;
            m_httpClientFactory = httpClientFactory;
            m_webhookReplayGuard = webhookReplayGuard;
            m_securityMetricsService = securityMetricsService;
            m_logger = logger;
        }

        /// <summary>
        /// Receive Midtrans payment notification for Production transactions.
        /// POST /api/midtrans/payment
        /// Always returns 200 OK to prevent Midtrans retries.
        /// </summary>
        [HttpPost("payment")]
        [EnableRateLimiting(RateLimitPolicyNames.WebhookTolerant)]
        public async Task<IActionResult> ProductionWebhook()
        {
            return await HandleWebhookAsync(m_midtransOptions.Production, "production");
        }

        /// <summary>
        /// Receive Midtrans payment notification for Sandbox transactions.
        /// POST /api/midtrans/sandbox/payment
        /// Always returns 200 OK to prevent Midtrans retries.
        /// </summary>
        [HttpPost("sandbox/payment")]
        [EnableRateLimiting(RateLimitPolicyNames.WebhookTolerant)]
        public async Task<IActionResult> SandboxWebhook()
        {
            return await HandleWebhookAsync(m_midtransOptions.Sandbox, "sandbox");
        }

        private async Task<IActionResult> HandleWebhookAsync(
            MidtransEnvironmentOptions envOptions,
            string midtransEnv)
        {
            // 1. If env is disabled, acknowledge silently (avoid Midtrans retries)
            if (!envOptions.IsEnabled)
            {
                m_logger.LogWarning("Received Midtrans {Env} webhook but environment is disabled. Acknowledging.", midtransEnv);
                return Ok();
            }

            // 2. Read raw body
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                m_logger.LogWarning("Received empty Midtrans {Env} webhook body.", midtransEnv);
                return BadRequest();
            }

            // 3. Parse fields needed for signature verification
            string orderId, statusCode, grossAmount, signatureKey, transactionStatus, transactionId, transactionTime;
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                orderId = GetStringOrEmpty(root, "order_id");
                statusCode = GetStringOrEmpty(root, "status_code");
                grossAmount = GetStringOrEmpty(root, "gross_amount");
                signatureKey = GetStringOrEmpty(root, "signature_key");
                transactionStatus = GetStringOrEmpty(root, "transaction_status");
                transactionId = GetStringOrEmpty(root, "transaction_id");
                transactionTime = GetStringOrEmpty(root, "transaction_time");
            }
            catch (JsonException ex)
            {
                m_logger.LogWarning(ex, "Failed to parse Midtrans {Env} webhook body.", midtransEnv);
                return BadRequest();
            }

            // 4. Minimum payload validation
            if (HasMissingRequiredFields(orderId, statusCode, grossAmount, signatureKey, transactionStatus, transactionId))
            {
                m_logger.LogWarning(
                    "Midtrans {Env} webhook rejected: missing required fields. order_id={OrderId}, transaction_id={TransactionId}",
                    midtransEnv,
                    string.IsNullOrWhiteSpace(orderId) ? "<missing>" : orderId,
                    string.IsNullOrWhiteSpace(transactionId) ? "<missing>" : transactionId);
                return BadRequest();
            }

            // 5. Verify signature
            if (!MidtransSignatureHelper.Verify(orderId, statusCode, grossAmount, signatureKey, envOptions.ServerKey))
            {
                m_securityMetricsService.Increment("webhook_invalid_signature_total", midtransEnv);
                m_logger.LogWarning(
                    "Midtrans {Env} webhook signature verification failed for order_id: {OrderId}",
                    midtransEnv, orderId);
                return BadRequest();
            }

            // 6. Anti-replay guard based on transaction_time
            if (!TryValidateReplayWindow(transactionTime, out var replayReason))
            {
                m_securityMetricsService.Increment("webhook_replay_suspected_total", midtransEnv);
                m_logger.LogWarning(
                    "Midtrans {Env} webhook rejected by replay guard for order_id {OrderId}. Reason: {Reason}",
                    midtransEnv,
                    orderId,
                    replayReason);
                return BadRequest();
            }

            // 7. Idempotency guard (duplicate notifications are acknowledged without reprocessing)
            var dedupeKey = $"midtrans:webhook:{orderId}:{transactionId}:{transactionStatus}".ToLowerInvariant();
            var dedupeTtl = TimeSpan.FromMinutes(Math.Max(1, m_webhookHardeningOptions.DeduplicationWindowMinutes));
            if (!m_webhookReplayGuard.TryAcquire(dedupeKey, dedupeTtl))
            {
                m_securityMetricsService.Increment("webhook_duplicate_total", midtransEnv);
                m_logger.LogInformation(
                    "Midtrans {Env} duplicate webhook acknowledged for order_id {OrderId}, transaction_id {TransactionId}, status {Status}",
                    midtransEnv, orderId, transactionId, transactionStatus);
                return Ok();
            }

            // 8. Look up Snap transaction log by MidtransOrderId
            var snapTransaction = await m_dbContext.SnapTransactions
                .Include(t => t.Environment)
                .FirstOrDefaultAsync(t => t.MidtransOrderId == orderId);

            if (snapTransaction == null)
            {
                m_logger.LogWarning(
                    "Midtrans {Env} webhook received for unknown order_id: {OrderId}. Acknowledging.",
                    midtransEnv, orderId);
                return Ok();
            }

            // 9. Update transaction status
            snapTransaction.TransactionStatus = transactionStatus;
            snapTransaction.MidtransTransactionId = transactionId;
            snapTransaction.UpdatedAt = DateTime.UtcNow;
            await m_dbContext.SaveChangesAsync();

            // 10. Forward notification to child app's registered WebhookUrl
            var webhookUrl = snapTransaction.Environment?.WebhookUrl;
            if (!string.IsNullOrWhiteSpace(webhookUrl))
            {
                if (!await IsWebhookUrlSafeAsync(webhookUrl))
                {
                    m_logger.LogWarning(
                        "Skipping webhook forward for order {OrderId}: WebhookUrl '{Url}' is not allowed (must be https and public-routable).",
                        orderId, webhookUrl);
                }
                else
                {
                    try
                    {
                        var forwardStatus = await SendForwardWithRetryAsync(webhookUrl, rawBody, HttpContext.RequestAborted);
                        if (forwardStatus != null)
                        {
                            m_logger.LogInformation(
                                "Forwarded Midtrans {Env} webhook for order {OrderId} to {Url}. Response: {Status}",
                                midtransEnv, orderId, webhookUrl, forwardStatus);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and continue — do not fail the Midtrans acknowledgement
                        m_logger.LogError(ex,
                            "Failed to forward Midtrans {Env} webhook for order {OrderId} to {Url}",
                            midtransEnv, orderId, webhookUrl);
                    }
                }
            }
            else
            {
                m_logger.LogInformation(
                    "No WebhookUrl registered for environment {EnvId}. Skipping forwarding for order {OrderId}.",
                    snapTransaction.EnvironmentId, orderId);
            }

            // 11. Always acknowledge to Midtrans
            return Ok();
        }

        private static string GetStringOrEmpty(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var el) ? el.GetString() ?? string.Empty : string.Empty;
        }

        private static bool HasMissingRequiredFields(
            string orderId,
            string statusCode,
            string grossAmount,
            string signatureKey,
            string transactionStatus,
            string transactionId)
        {
            return string.IsNullOrWhiteSpace(orderId)
                || string.IsNullOrWhiteSpace(statusCode)
                || string.IsNullOrWhiteSpace(grossAmount)
                || string.IsNullOrWhiteSpace(signatureKey)
                || string.IsNullOrWhiteSpace(transactionStatus)
                || string.IsNullOrWhiteSpace(transactionId);
        }

        private bool TryValidateReplayWindow(string transactionTimeRaw, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(transactionTimeRaw))
            {
                if (m_webhookHardeningOptions.RejectWhenTransactionTimeMissing)
                {
                    reason = "transaction_time is required";
                    return false;
                }

                return true;
            }

            if (!TryParseMidtransTransactionTimeToUtc(transactionTimeRaw, out var txTimeUtc))
            {
                reason = "transaction_time format is invalid";
                return false;
            }

            var replayWindow = TimeSpan.FromMinutes(Math.Max(1, m_webhookHardeningOptions.ReplayWindowMinutes));
            var now = DateTime.UtcNow;
            if (txTimeUtc < now - replayWindow)
            {
                reason = "transaction_time too old";
                return false;
            }

            if (txTimeUtc > now + replayWindow)
            {
                reason = "transaction_time too far in future";
                return false;
            }

            return true;
        }

        private static bool TryParseMidtransTransactionTimeToUtc(string rawValue, out DateTime utc)
        {
            utc = default;
            if (!DateTime.TryParseExact(
                rawValue,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return false;
            }

            // Midtrans transaction_time documented in WIB (UTC+7).
            var dto = new DateTimeOffset(parsed, TimeSpan.FromHours(7));
            utc = dto.UtcDateTime;
            return true;
        }

        private async Task<bool> IsWebhookUrlSafeAsync(string url)
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

        private async Task<HttpStatusCode?> SendForwardWithRetryAsync(string webhookUrl, string rawBody, CancellationToken cancellationToken)
        {
            var client = m_httpClientFactory.CreateClient("webhook-forward");
            var maxRetries = Math.Max(0, m_webhookHardeningOptions.ForwardRetryCount);
            var retryDelay = TimeSpan.FromMilliseconds(Math.Max(50, m_webhookHardeningOptions.ForwardRetryDelayMs));

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                using var forwardRequest = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
                {
                    Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
                };

                try
                {
                    using var response = await client.SendAsync(forwardRequest, cancellationToken);
                    if ((int)response.StatusCode >= 500 && attempt < maxRetries)
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                        continue;
                    }

                    return response.StatusCode;
                }
                catch when (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            return null;
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
