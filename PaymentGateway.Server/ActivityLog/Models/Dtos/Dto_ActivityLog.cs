namespace PaymentGateway.Server.ActivityLog.Models.Dtos
{
    public class Dto_ActivityLogItem
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? SessionToken { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
