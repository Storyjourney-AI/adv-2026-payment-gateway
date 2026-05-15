using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Common.Interfaces;

namespace PaymentGateway.Server.Applications.Models.Dbs
{
    public class Db_Application : ISoftDelete
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public Db_ApplicationUser? User { get; set; }
        public List<Db_Environment> Environments { get; set; } = new();
    }
}
