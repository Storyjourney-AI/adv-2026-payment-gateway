namespace PaymentGateway.Server.Applications.Models.Dtos
{
    public class Dto_ApplicationListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EnvironmentCount { get; set; }
    }
}
