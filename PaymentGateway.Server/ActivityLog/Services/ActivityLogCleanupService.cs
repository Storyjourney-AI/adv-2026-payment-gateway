using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.Databases;

namespace PaymentGateway.Server.ActivityLog.Services
{
    public class ActivityLogCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory m_scopeFactory;
        private readonly ILogger<ActivityLogCleanupService> m_logger;
        private readonly TimeSpan m_cleanupInterval = TimeSpan.FromHours(24);
        private readonly int m_retentionDays = 30;

        public ActivityLogCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<ActivityLogCleanupService> logger)
        {
            m_scopeFactory = scopeFactory;
            m_logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            m_logger.LogInformation("Activity log cleanup service started. Retention: {Days} days, Interval: {Hours}h",
                m_retentionDays, m_cleanupInterval.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldLogsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    m_logger.LogError(ex, "Error during activity log cleanup");
                }

                await Task.Delay(m_cleanupInterval, stoppingToken);
            }
        }

        private async Task CleanupOldLogsAsync(CancellationToken stoppingToken)
        {
            using var scope = m_scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-m_retentionDays);
            var deletedCount = await dbContext.ActivityLogs
                .Where(log => log.Timestamp < cutoffDate)
                .ExecuteDeleteAsync(stoppingToken);

            if (deletedCount > 0)
            {
                m_logger.LogInformation("Activity log cleanup: deleted {Count} logs older than {Days} days", deletedCount, m_retentionDays);
            }
        }
    }
}
