using Meridian.Application.Quotes;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

internal sealed class SecurityCatalog(MeridianDbContext db) : ISecurityCatalog
{
    public async Task<IReadOnlyList<Security>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Securities.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
}
