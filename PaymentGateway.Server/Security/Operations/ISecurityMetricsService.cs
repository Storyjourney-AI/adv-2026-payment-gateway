namespace PaymentGateway.Server.Security.Operations
{
    public interface ISecurityMetricsService
    {
        void Increment(string metricName, string? dimension = null);
        IReadOnlyList<SecurityMetricSnapshot> GetSnapshots();
    }

    public sealed record SecurityMetricSnapshot(string MetricName, string Dimension, long Count);
}
