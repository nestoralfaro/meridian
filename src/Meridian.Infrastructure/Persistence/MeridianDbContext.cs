using Meridian.Domain;
using Meridian.Infrastructure.Identity;
using Meridian.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

/// <summary>
/// The application's database: domain data, Identity users (no roles), and Data Protection keys, which are shared
/// by all replicas so cookies and antiforgery tokens survive restarts and scale-out.
/// </summary>
internal sealed class MeridianDbContext(DbContextOptions<MeridianDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>The PostgreSQL role the running application connects as (see db/app-role-grants.sql).</summary>
    public const string ApplicationRole = "meridian_app";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Security> Securities => Set<Security>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<DemoDataState> DemoDataState => Set<DemoDataState>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tables
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeridianDbContext).Assembly);

        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(100);
            user.HasOne<Client>().WithMany().HasForeignKey(u => u.ClientId).OnDelete(DeleteBehavior.Cascade);
            user.HasIndex(u => u.ClientId);
        });

        modelBuilder.Entity<AuditEvent>(audit =>
        {
            audit.ToTable("AuditEvents");
            audit.HasKey(e => e.Id);
            audit.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
            audit.Property(e => e.Actor).HasMaxLength(200);
            audit.Property(e => e.Action).HasMaxLength(100);
            audit.Property(e => e.Subject).HasMaxLength(200);
            audit.Property(e => e.CorrelationId).HasMaxLength(100);
            audit.Property(e => e.Data).HasColumnType("jsonb");
            audit.HasIndex(e => new { e.Subject, e.OccurredAt });
        });
    }
}
