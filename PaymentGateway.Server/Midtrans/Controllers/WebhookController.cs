using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Midtrans.Utils;  // MidtransSignatureHelper
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.RateLimiting;
using PaymentGateway.Server.Security.Webhook;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Midtrans.Controllers
{
    [ApiController]
    [Route("api/midtrans")]
    [AllowAnonymous]
    public class WebhookController : ControllerBase
    {
        private const string ReplayReasonTransactionTimeTooOld = "transaction_time too old";

        private readonly MidtransOptions m_midtransOptions;
        private readonly WebhookHardeningOptions m_webhookHardeningOptions;
        private readonly WebhookForwardRetryOptions m_webhookForwardRetryOptions;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly IWebhookReplayGuard m_webhookReplayGuard;
        private readonly ISecurityMetricsService m_securityMetricsService;
        private readonly IMidtransTransactionReconciliationService m_midtransTransactionReconciliationService;
        private readonly IWebhookUrlSafetyValidator m_webhookUrlSafetyValidator;
        private readonly IWebhookForwardService m_webhookForwardService;
        private readonly ILogger<WebhookController> m_logger;

        public WebhookController(
            IOptions<MidtransOptions> midtransOptions,
            IOptions<WebhookHardeningOptions> webhookHardeningOptions,
            IOptions<WebhookForwardRetryOptions> webhookForwardRetryOptions,
            IHttpClientFactory httpClientFactory,
            IWebhookReplayGuard webhookReplayGuard,
            ISecurityMetricsService securityMetricsService,
            IMidtransTransactionReconciliationService midtransTransactionReconciliationService,
            IWebhookUrlSafetyValidator webhookUrlSafetyValidator,
            IWebhookForwardService webhookForwardService,
            ILogger<WebhookController> logger)
        {
            m_midtransOptions = midtransOptions.Value;
            m_webhookHardeningOptions = webhookHardeningOptions.Value;
            m_webhookForwardRetryOptions = webhookForwardRetryOptions.Value;
            m_httpClientFactory = httpClientFactory;
            m_webhookReplayGuard = webhookReplayGuard;
            m_securityMetricsService = securityMetricsService;
            m_midtransTransactionReconciliationService = midtransTransactionReconciliationService;
            m_webhookUrlSafetyValidator = webhookUrlSafetyValidator;
            m_webhookForwardService = webhookForwardService;
            m_logger = logger;
        }

        /// <summary>
        /// Receive Midtrans payment notification for Production transactions.
        /// POST /api/midtrans/payment
        /// Valid notifications are acknowledged with 200 OK; malformed or invalid payloads can return 400 Bad Request.
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
        /// Valid notifications are acknowledged with 200 OK; malformed or invalid payloads can return 400 Bad Request.
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
                if (string.Equals(replayReason, ReplayReasonTransactionTimeTooOld, StringComparison.Ordinal))
                {
                    m_logger.LogInformation(
                        "Midtrans {Env} webhook transaction_time is outside the replay window for order_id {OrderId}. Continuing with reconciliation before acknowledging.",
                        midtransEnv,
                        orderId);
                }
                else
                {
                    m_securityMetricsService.Increment("webhook_replay_suspected_total", midtransEnv);
                    m_logger.LogWarning(
                        "Midtrans {Env} webhook rejected by replay guard for order_id {OrderId}. Reason: {Reason}",
                        midtransEnv,
                        orderId,
                        replayReason);
                    return BadRequest();
                }
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

            MidtransTransactionReconciliationResult? reconciliationResult;
            try
            {
                reconciliationResult = await m_midtransTransactionReconciliationService
                    .ReconcileByMidtransOrderIdAsync(orderId, HttpContext.RequestAborted);
            }
            catch (MidtransStatusVerificationException ex)
            {
                m_logger.LogWarning(
                    ex,
                    "Midtrans {Env} webhook status verification failed for order_id {OrderId}. Acknowledging without state update.",
                    midtransEnv,
                    orderId);
                return Ok();
            }

            if (reconciliationResult == null)
            {
                m_logger.LogWarning(
                    "Midtrans {Env} webhook received for unknown order_id: {OrderId}. Acknowledging.",
                    midtransEnv,
                    orderId);
                return Ok();
            }

            // 9. Forward notification to child app's registered WebhookUrl
            var webhookUrl = reconciliationResult.Environment.WebhookUrl;
            if (!string.IsNullOrWhiteSpace(webhookUrl))
            {
                if (!await m_webhookUrlSafetyValidator.IsWebhookUrlSafeAsync(webhookUrl))
                {
                    m_logger.LogWarning(
                        "Skipping webhook forward for order {OrderId}: WebhookUrl '{Url}' is not allowed (must be https and public-routable).",
                        orderId, webhookUrl);
                }
                else
                {
                    // Enqueue/upsert the outbox row so the background service can retry on failure.
                    // EnqueueAsync returns the tracked row, which we use to get the built payload.
                    Db_WebhookForwardOutbox? outboxRow = null;
                    try
                    {
                        outboxRow = await m_webhookForwardService.EnqueueAsync(
                            snapTransactionId: reconciliationResult.Transaction.Id,
                            environmentId: reconciliationResult.Transaction.EnvironmentId,
                            midtransOrderId: reconciliationResult.Transaction.MidtransOrderId,
                            callerOrderId: reconciliationResult.Transaction.CallerOrderId,
                            targetUrl: webhookUrl,
                            rawBody: rawBody,
                            verifiedStatus: reconciliationResult.VerifiedStatus,
                            maxAttempts: m_webhookForwardRetryOptions.MaxAttempts,
                            cancellationToken: HttpContext.RequestAborted);
                    }
                    catch (Exception ex)
                    {
                        m_logger.LogError(ex,
                            "Failed to enqueue webhook forward outbox row for order {OrderId}.",
                            orderId);
                    }

                    // Inline first-attempt forward using the payload already built by EnqueueAsync.
                    // On 2xx: call MarkDeliveredAsync (no second POST).
                    // On failure: leave the row as Pending so the drainer picks it up.
                    if (outboxRow != null)
                    {
                        try
                        {
                            var forwardStatus = await SendForwardWithRetryAsync(
                                webhookUrl, outboxRow.Payload, HttpContext.RequestAborted);

                            if (forwardStatus.HasValue && (int)forwardStatus.Value >= 200 && (int)forwardStatus.Value < 300)
                            {
                                // Inline delivery succeeded — mark delivered WITHOUT a second POST.
                                try
                                {
                                    await m_webhookForwardService.MarkDeliveredAsync(
                                        reconciliationResult.Transaction.Id,
                                        (int)forwardStatus.Value,
                                        HttpContext.RequestAborted);
                                }
                                catch (Exception ex)
                                {
                                    m_logger.LogWarning(ex,
                                        "Failed to mark outbox row Delivered after inline forward success for order {OrderId}.",
                                        orderId);
                                }

                                m_logger.LogInformation(
                                    "Forwarded Midtrans {Env} webhook for order {OrderId} to {Url}. Response: {Status}",
                                    midtransEnv, orderId, webhookUrl, forwardStatus);
                            }
                            else
                            {
                                // Inline forward failed — outbox row stays Pending with NextAttemptAt=now;
                                // the retry service will pick it up on the next drain cycle.
                                m_logger.LogWarning(
                                    "Inline webhook forward failed for order {OrderId} to {Url}. Response: {Status}. Outbox retry scheduled.",
                                    orderId, webhookUrl, forwardStatus?.ToString() ?? "none");
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
            }
            else
            {
                m_logger.LogInformation(
                    "No WebhookUrl registered for environment {EnvId}. Skipping forwarding for order {OrderId}.",
                    reconciliationResult.Transaction.EnvironmentId, orderId);
            }

            // 10. Always acknowledge to Midtrans
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
                reason = ReplayReasonTransactionTimeTooOld;
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
    }
}
