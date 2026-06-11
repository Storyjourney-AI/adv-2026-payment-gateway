using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;

namespace PaymentGateway.Server.Midtrans.Services
{
    public sealed class WebhookForwardRetryService : BackgroundService
    {
        private readonly IServiceScopeFactory m_scopeFactory;
        private readonly WebhookForwardRetryOptions m_options;
        private readonly ILogger<WebhookForwardRetryService> m_logger;

        public WebhookForwardRetryService(
            IServiceScopeFactory scopeFactory,
            IOptions<WebhookForwardRetryOptions> options,
            ILogger<WebhookForwardRetryService> logger)
        {
            m_scopeFactory = scopeFactory;
            m_options = options.Value;
            m_logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            m_logger.LogInformation(
                "WebhookForwardRetryService started. Interval: {Interval}s, MaxAttempts: {MaxAttempts}, BatchSize: {BatchSize}",
                m_options.IntervalSeconds, m_options.MaxAttempts, m_options.BatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DrainDueRowsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    m_logger.LogError(ex, "Error during webhook forward retry cycle.");
                }

                await Task.Delay(TimeSpan.FromSeconds(m_options.IntervalSeconds), stoppingToken);
            }
        }

        private async Task DrainDueRowsAsync(CancellationToken stoppingToken)
        {
            using var scope = m_scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var forwardService = scope.ServiceProvider.GetRequiredService<IWebhookForwardService>();

            var now = DateTime.UtcNow;
            // InProgressLeaseSeconds must exceed the "webhook-forward" HttpClient timeout (currently 10 s)
            // to prevent double-delivery while a slow-but-alive POST is still in flight.
            var leaseExpiry = now - TimeSpan.FromSeconds(m_options.InProgressLeaseSeconds);

            // Load due rows: Pending/Failed due now, OR InProgress rows whose lease has expired (stale/crashed).
            // Exclude rows whose Environment or Application is soft-deleted (HasQueryFilter handles that for joined
            // navigations; we materialise via a join to avoid forwarding to decommissioned tenants).
            var dueRows = await dbContext.WebhookForwardOutbox
                .Join(
                    dbContext.SnapTransactions
                        .Include(t => t.Environment)
                            .ThenInclude(e => e!.Application),
                    outbox => outbox.SnapTransactionId,
                    tx => tx.Id,
                    (outbox, tx) => new { outbox, tx })
                .Where(x =>
                    x.tx.Environment != null &&
                    x.tx.Environment.Application != null &&
                    (
                        // Normal due rows
                        ((x.outbox.Status == WebhookForwardStatus.Pending || x.outbox.Status == WebhookForwardStatus.Failed)
                            && x.outbox.NextAttemptAt <= now)
                        ||
                        // Stale InProgress rows whose lease has expired
                        (x.outbox.Status == WebhookForwardStatus.InProgress
                            && x.outbox.LastAttemptAt != null
                            && x.outbox.LastAttemptAt <= leaseExpiry)
                    ))
                .OrderBy(x => x.outbox.NextAttemptAt)
                .Take(m_options.BatchSize)
                .Select(x => x.outbox)
                .ToListAsync(stoppingToken);

            if (dueRows.Count == 0)
            {
                return;
            }

            m_logger.LogInformation("WebhookForwardRetryService: processing {Count} due row(s).", dueRows.Count);

            foreach (var row in dueRows)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await forwardService.TryDeliverAsync(row, m_options, stoppingToken);
                }
                catch (Exception ex)
                {
                    m_logger.LogError(ex,
                        "Unhandled error delivering outbox row {Id} (order {OrderId}).",
                        row.Id, row.MidtransOrderId);
                }
            }
        }
    }
}
