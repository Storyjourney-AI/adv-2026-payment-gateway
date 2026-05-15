using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.ActivityLog.Models.Dbs;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dbs;

namespace PaymentGateway.Server.Databases
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Db_ApplicationUser, Db_ApplicationRole, Guid>(options)
    {
        public DbSet<Db_RefreshToken> RefreshTokens { get; set; }
        public DbSet<Db_Application> Applications { get; set; }
        public DbSet<Db_Environment> Environments { get; set; }
        public DbSet<Db_SnapTransaction> SnapTransactions { get; set; }
        public DbSet<Db_ActivityLog> ActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // configure auth schema
            ConfigureAuthSchema(builder);
            
            // configure payment schema
            ConfigurePaymentSchema(builder);

            // configure midtrans schema
            ConfigureMidtransSchema(builder);

            // configure audit schema
            ConfigureAuditSchema(builder);
        }

        private void ConfigureAuthSchema(ModelBuilder builder)
        {
            builder.Entity<Db_ApplicationUser>().ToTable("AspNetUsers", "auth");
            builder.Entity<Db_ApplicationRole>().ToTable("AspNetRoles", "auth");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("AspNetUserRoles", "auth");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("AspNetUserClaims", "auth");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AspNetRoleClaims", "auth");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("AspNetUserLogins", "auth");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("AspNetUserTokens", "auth");

            // Configure Refresh Token entity
            builder.Entity<Db_RefreshToken>()
                .ToTable("RefreshTokens", "auth");

            builder.Entity<Db_RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();

            builder.Entity<Db_RefreshToken>()
                .HasIndex(rt => rt.UserId);

            builder.Entity<Db_RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigurePaymentSchema(ModelBuilder builder)
        {
            // Configure Applications table
            builder.Entity<Db_Application>()
                .ToTable("Applications", "payment");

            builder.Entity<Db_Application>()
                .HasQueryFilter(a => !a.IsDeleted);

            builder.Entity<Db_Application>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Db_Application>()
                .HasMany(a => a.Environments)
                .WithOne(e => e.Application)
                .HasForeignKey(e => e.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Environments table
            builder.Entity<Db_Environment>()
                .ToTable("Environments", "payment");

            builder.Entity<Db_Environment>()
                .HasQueryFilter(e => !e.IsDeleted);

            builder.Entity<Db_Environment>()
                .HasIndex(e => e.ApiKey)
                .IsUnique();
        }

        private void ConfigureMidtransSchema(ModelBuilder builder)
        {
            builder.Entity<Db_SnapTransaction>()
                .ToTable("SnapTransactions", "payment");

            builder.Entity<Db_SnapTransaction>()
                .HasIndex(t => t.MidtransOrderId)
                .IsUnique();

            builder.Entity<Db_SnapTransaction>()
                .HasIndex(t => new { t.EnvironmentId, t.CallerOrderId })
                .IsUnique()
                .HasDatabaseName("IX_SnapTransactions_EnvironmentId_CallerOrderId");

            builder.Entity<Db_SnapTransaction>()
                .HasOne(t => t.Environment)
                .WithMany()
                .HasForeignKey(t => t.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureAuditSchema(ModelBuilder builder)
        {
            builder.Entity<Db_ActivityLog>()
                .ToTable("ActivityLogs", "audit");

            builder.Entity<Db_ActivityLog>()
                .HasIndex(l => l.Timestamp);

            builder.Entity<Db_ActivityLog>()
                .HasIndex(l => l.UserId);

            builder.Entity<Db_ActivityLog>()
                .HasIndex(l => l.Category);
        }
      }

}
