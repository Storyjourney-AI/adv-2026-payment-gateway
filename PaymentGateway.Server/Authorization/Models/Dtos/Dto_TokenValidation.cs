namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    public class Dto_TokenValidation
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public bool IsValid { get; set; }
    }

}
