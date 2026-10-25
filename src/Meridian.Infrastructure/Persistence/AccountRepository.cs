using Meridian.Application.Accounts;
using Meridian.Domain;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

internal sealed class AccountRepository(MeridianDbContext db) : IAccountRepository
{
    public async Task<IReadOnlyList<Account>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        await WithHoldings().Where(a => a.ClientId == clientId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Account?> FindAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        WithHoldings().SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    private IQueryable<Account> WithHoldings() =>
        db.Accounts.AsNoTracking().AsSplitQuery().Include(a => a.Positions).ThenInclude(p => p.Security);
}
