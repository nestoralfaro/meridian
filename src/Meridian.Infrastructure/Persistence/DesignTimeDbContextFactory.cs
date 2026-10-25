using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Meridian.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> (adding migrations, building the migration bundle). The bundle receives the real
/// connection string at run time via <c>--connection</c>; this default is never used to reach a database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MeridianDbContext>
{
    public MeridianDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MeridianDbContext>()
            .UseNpgsql("Host=localhost;Database=meridian;Username=meridian")
            .Options);
}
