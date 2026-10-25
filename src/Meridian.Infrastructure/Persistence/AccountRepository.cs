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

    public async Task<IReadOnlyList<(Transaction Transaction, string AccountName)>> RecentTransactionsAsync(Guid clientId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await db.Transactions.AsNoTracking()
            .Join(db.Accounts.Where(a => a.ClientId == clientId), t => t.AccountId, a => a.Id, (t, a) => new { Transaction = t, AccountName = a.Name })
            .OrderByDescending(r => r.Transaction.Date).ThenByDescending(r => r.Transaction.Id)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(r => (r.Transaction, r.AccountName))];
    }

    private IQueryable<Account> WithHoldings() =>
        db.Accounts.AsNoTracking().AsSplitQuery().Include(a => a.Positions).ThenInclude(p => p.Security);
}
