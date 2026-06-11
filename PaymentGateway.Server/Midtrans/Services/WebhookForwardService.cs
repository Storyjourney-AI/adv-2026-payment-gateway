using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Utils;
using PaymentGateway.Server.Security.Webhook;
using System.Net;
using System.Text;

namespace PaymentGateway.Server.Midtrans.Services
{
    public interface IWebhookForwardService
    {
        /// <summary>
        /// Upserts the single outbox row for <paramref name="snapTransactionId"/>, setting Status=Pending
        /// and storing the forward payload. Safe to call on every reconciled notification.
        /// Terminal rows (Delivered / Exhausted) are only reset to Pending when the payload has actually changed.
        /// </summary>
        Task<Db_WebhookForwardOutbox> EnqueueAsync(
            Guid snapTransactionId,
            Guid environmentId,
            string midtransOrderId,
            string callerOrderId,
            string targetUrl,
            string rawBody,
            MidtransVerifiedStatus verifiedStatus,
            int maxAttempts,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims the row (transitions to InProgress) and POSTs the stored payload to the target URL.
        /// Transitions to Delivered on 2xx; increments AttemptCount and schedules a backoff retry on failure,
        /// or marks Exhausted when AttemptCount >= MaxAttempts.
        /// Skips on <see cref="DbUpdateConcurrencyException"/> (another worker already claimed the row).
        /// </summary>
        Task TryDeliverAsync(
            Db_WebhookForwardOutbox row,
            WebhookForwardRetryOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks an already-delivered row as Delivered without performing any HTTP call.
        /// Used when the inline path has already successfully forwarded the notification.
        /// </summary>
        Task MarkDeliveredAsync(
            Guid snapTransactionId,
            int statusCode,
            CancellationToken cancellationToken = default);
    }

    public sealed class WebhookForwardService : IWebhookForwardService
    {
        private readonly AppDbContext m_dbContext;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly IWebhookUrlSafetyValidator m_urlSafetyValidator;
        private readonly ILogger<WebhookForwardService> m_logger;
        private readonly WebhookForwardRetryOptions m_options;

        public WebhookForwardService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IWebhookUrlSafetyValidator urlSafetyValidator,
            ILogger<WebhookForwardService> logger,
            IOptions<WebhookForwardRetryOptions> options)
        {
            m_dbContext = dbContext;
            m_httpClientFactory = httpClientFactory;
            m_urlSafetyValidator = urlSafetyValidator;
            m_logger = logger;
            m_options = options.Value;
        }

        public async Task<Db_WebhookForwardOutbox> EnqueueAsync(
            Guid snapTransactionId,
            Guid environmentId,
            string midtransOrderId,
            string callerOrderId,
            string targetUrl,
            string rawBody,
            MidtransVerifiedStatus verifiedStatus,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            var newPayload = MidtransWebhookForwardPayloadBuilder.Build(rawBody, verifiedStatus.FeeBreakdown);

            var existing = await m_dbContext.WebhookForwardOutbox
                .FirstOrDefaultAsync(o => o.SnapTransactionId == snapTransactionId, cancellationToken);

            var now = DateTime.UtcNow;

            if (existing != null)
            {
                var isTerminal = existing.Status == WebhookForwardStatus.Delivered
                    || existing.Status == WebhookForwardStatus.Exhausted;

                if (isTerminal)
                {
                    // Only re-arm a terminal row if the payload actually changed
                    // (e.g. a genuine pending→settlement transition with different status).
                    // An identical duplicate must NOT re-forward.
                    if (string.Equals(existing.Payload, newPayload, StringComparison.Ordinal)
                        && string.Equals(existing.RawNotificationBody, rawBody, StringComparison.Ordinal))
                    {
                        // Identical payload — leave the terminal row untouched
                        return existing;
                    }
                }

                // Reset to Pending (either non-terminal, or terminal with changed payload)
                existing.Status = WebhookForwardStatus.Pending;
                existing.TargetUrl = targetUrl;
                existing.Payload = newPayload;
                existing.RawNotificationBody = rawBody;
                existing.MaxAttempts = maxAttempts;
                existing.NextAttemptAt = now;
                existing.UpdatedAt = now;
                await m_dbContext.SaveChangesAsync(cancellationToken);
                return existing;
            }
            else
            {
                var outbox = new Db_WebhookForwardOutbox
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    SnapTransactionId = snapTransactionId,
                    MidtransOrderId = midtransOrderId,
                    CallerOrderId = callerOrderId,
                    TargetUrl = targetUrl,
                    Payload = newPayload,
                    RawNotificationBody = rawBody,
                    Status = WebhookForwardStatus.Pending,
                    AttemptCount = 0,
                    MaxAttempts = maxAttempts,
                    NextAttemptAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await m_dbContext.WebhookForwardOutbox.AddAsync(outbox, cancellationToken);
                await m_dbContext.SaveChangesAsync(cancellationToken);
                return outbox;
            }
        }

        public async Task TryDeliverAsync(
            Db_WebhookForwardOutbox row,
            WebhookForwardRetryOptions options,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            // ── Claim the row (Pending/Failed → InProgress) before POSTing ──────
            // This prevents a concurrent drainer or manual retry from picking up the same row.
            row.Status = WebhookForwardStatus.InProgress;
            row.LastAttemptAt = now;
            row.UpdatedAt = now;

            try
            {
                await m_dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                m_logger.LogInformation(
                    "Outbox row {Id} (order {OrderId}) was claimed by a concurrent worker — skipping.",
                    row.Id, row.MidtransOrderId);
                return;
            }

            // Re-check SSRF safety before every send
            if (!await m_urlSafetyValidator.IsWebhookUrlSafeAsync(row.TargetUrl))
            {
                m_logger.LogWarning(
                    "Webhook forward skipped for outbox row {Id} (order {OrderId}): TargetUrl '{Url}' failed SSRF check.",
                    row.Id, row.MidtransOrderId, row.TargetUrl);

                row.AttemptCount++;
                row.LastError = "SSRF check failed: URL is not safe to forward to.";
                row.LastResponseCode = null;
                row.UpdatedAt = DateTime.UtcNow;

                TransitionOnFailure(row, options, DateTime.UtcNow);
                await m_dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            HttpStatusCode? responseCode = null;
            string? errorMessage = null;

            try
            {
                var client = m_httpClientFactory.CreateClient("webhook-forward");
                using var request = new HttpRequestMessage(HttpMethod.Post, row.TargetUrl)
                {
                    Content = new StringContent(row.Payload, Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request, cancellationToken);
                responseCode = response.StatusCode;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                m_logger.LogWarning(ex,
                    "Webhook forward network error for outbox row {Id} (order {OrderId}) to {Url}.",
                    row.Id, row.MidtransOrderId, row.TargetUrl);
            }

            now = DateTime.UtcNow;
            row.AttemptCount++;
            row.LastAttemptAt = now;
            row.LastResponseCode = responseCode.HasValue ? (int)responseCode.Value : null;
            row.LastError = errorMessage;
            row.UpdatedAt = now;

            if (responseCode.HasValue && (int)responseCode.Value >= 200 && (int)responseCode.Value < 300)
            {
                row.Status = WebhookForwardStatus.Delivered;
                row.NextAttemptAt = null;

                m_logger.LogInformation(
                    "Webhook forward delivered for outbox row {Id} (order {OrderId}). Response: {Status}",
                    row.Id, row.MidtransOrderId, responseCode.Value);
            }
            else
            {
                TransitionOnFailure(row, options, now);

                m_logger.LogWarning(
                    "Webhook forward failed for outbox row {Id} (order {OrderId}). Response: {Status}, Error: {Error}, AttemptCount: {Count}, Status: {RowStatus}",
                    row.Id, row.MidtransOrderId, responseCode?.ToString() ?? "none", errorMessage ?? "none", row.AttemptCount, row.Status);
            }

            try
            {
                await m_dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent worker (stale-lease reclaim path) won the same row's terminal write.
                // Reload to confirm the row already reached a terminal state; if so, treat as success.
                // Otherwise log and return without throwing so the drainer/manual-retry path stays healthy.
                await m_dbContext.Entry(row).ReloadAsync(cancellationToken);
                var isAlreadyTerminal = row.Status == WebhookForwardStatus.Delivered
                    || row.Status == WebhookForwardStatus.Exhausted;
                if (!isAlreadyTerminal)
                {
                    m_logger.LogWarning(
                        "Terminal state-write concurrency conflict on outbox row {Id} (order {OrderId}). " +
                        "Row is not yet terminal after reload — will be re-evaluated on the next drain cycle.",
                        row.Id, row.MidtransOrderId);
                }
                // Either way, do not rethrow — the caller gets a clean return and the row
                // will be re-evaluated or is already done.
            }
        }

        public async Task MarkDeliveredAsync(
            Guid snapTransactionId,
            int statusCode,
            CancellationToken cancellationToken = default)
        {
            var row = await m_dbContext.WebhookForwardOutbox
                .FirstOrDefaultAsync(o => o.SnapTransactionId == snapTransactionId, cancellationToken);

            if (row == null)
            {
                m_logger.LogWarning(
                    "MarkDeliveredAsync: outbox row not found for SnapTransactionId {Id}.", snapTransactionId);
                return;
            }

            var now = DateTime.UtcNow;
            row.Status = WebhookForwardStatus.Delivered;
            row.AttemptCount++;
            row.LastAttemptAt = now;
            row.LastResponseCode = statusCode;
            row.LastError = null;
            row.NextAttemptAt = null;
            row.UpdatedAt = now;

            await m_dbContext.SaveChangesAsync(cancellationToken);

            m_logger.LogInformation(
                "Outbox row {Id} (order {OrderId}) marked Delivered via inline forward. Response: {Status}",
                row.Id, row.MidtransOrderId, statusCode);
        }

        /// <summary>
        /// Returns true if a row stuck in InProgress is reclaimable (lease has expired).
        /// </summary>
        public bool IsInProgressReclaimable(Db_WebhookForwardOutbox row, DateTime now)
        {
            var leaseWindow = TimeSpan.FromSeconds(m_options.InProgressLeaseSeconds);
            return row.Status == WebhookForwardStatus.InProgress
                && row.LastAttemptAt.HasValue
                && now - row.LastAttemptAt.Value > leaseWindow;
        }

        private static void TransitionOnFailure(Db_WebhookForwardOutbox row, WebhookForwardRetryOptions options, DateTime now)
        {
            if (row.AttemptCount >= options.MaxAttempts)
            {
                row.Status = WebhookForwardStatus.Exhausted;
                row.NextAttemptAt = null;
            }
            else
            {
                row.Status = WebhookForwardStatus.Failed;
                // Exponential backoff: first retry uses AttemptCount-1 so attempt=1 → 30s, attempt=2 → 60s, etc.
                // Formula: min(MaxBackoffSeconds, BaseBackoffSeconds * 2^(AttemptCount-1))
                var backoffSeconds = Math.Min(
                    options.MaxBackoffSeconds,
                    options.BaseBackoffSeconds * Math.Pow(2, row.AttemptCount - 1));
                row.NextAttemptAt = now.AddSeconds(backoffSeconds);
            }
        }
    }
}
