using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.ActivityLog.Models.Dbs
{
    public class Db_ActivityLog
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// The user who performed the action
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// User email for display purposes
        /// </summary>
        [MaxLength(256)]
        public string? UserEmail { get; set; }

        /// <summary>
        /// First 8 characters of the JWT token used for this session
        /// </summary>
        [MaxLength(8)]
        public string? SessionToken { get; set; }

        /// <summary>
        /// Category of the action: login, creation, modification, deletion
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Concise description of the action performed
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// When the action was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
