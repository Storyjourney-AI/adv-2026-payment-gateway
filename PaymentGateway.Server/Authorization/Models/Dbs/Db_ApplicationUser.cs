using Microsoft.AspNetCore.Identity;

namespace PaymentGateway.Server.Authorization.Models.Dbs
{
    public class Db_ApplicationUser : IdentityUser<Guid>
    {
        /// <summary>
        /// User's full name (optional)
        /// </summary>
        public string? FullName { get; set; }
        
        public DateTime RegisterAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
