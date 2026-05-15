namespace PaymentGateway.Server.Applications.Models.Dtos
{
    public class Dto_ApplicationResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<Dto_EnvironmentResponse> Environments { get; set; } = new();
    }
}
