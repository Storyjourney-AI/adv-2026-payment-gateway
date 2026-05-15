using Microsoft.AspNetCore.Identity;

namespace PaymentGateway.Server.Authorization.Models.Dbs
{
    public class Db_ApplicationRole : IdentityRole<Guid>
    {
        public bool IsSystemRole { get; set; } = false;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
