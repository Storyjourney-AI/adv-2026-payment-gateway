using System.Collections.Concurrent;

namespace PaymentGateway.Server.Security.Operations
{
    public sealed class SecurityMetricsService : ISecurityMetricsService
    {
        private readonly ConcurrentDictionary<string, long> m_counters = new(StringComparer.OrdinalIgnoreCase);

        public void Increment(string metricName, string? dimension = null)
        {
            var safeMetric = string.IsNullOrWhiteSpace(metricName) ? "unknown_metric" : metricName.Trim();
            var safeDimension = string.IsNullOrWhiteSpace(dimension) ? "global" : dimension.Trim();
            var key = $"{safeMetric}||{safeDimension}";
            m_counters.AddOrUpdate(key, 1, (_, current) => current + 1);
        }

        public IReadOnlyList<SecurityMetricSnapshot> GetSnapshots()
        {
            return m_counters
                .Select(kvp =>
                {
                    var split = kvp.Key.Split("||", 2, StringSplitOptions.None);
                    var metricName = split.Length > 0 ? split[0] : "unknown_metric";
                    var dimension = split.Length > 1 ? split[1] : "global";
                    return new SecurityMetricSnapshot(metricName, dimension, kvp.Value);
                })
                .OrderBy(x => x.MetricName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Dimension, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
