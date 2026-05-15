using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dbs
{
    public class Db_RefreshToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Token { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        public Db_ApplicationUser? User { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
