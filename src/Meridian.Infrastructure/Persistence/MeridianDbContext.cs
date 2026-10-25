using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

internal sealed class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    /// <summary>The PostgreSQL role the running application connects as (see db/app-role-grants.sql).</summary>
    public const string ApplicationRole = "meridian_app";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
