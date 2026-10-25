using Meridian.Application.DemoData;
using Meridian.Infrastructure.Persistence;
using Meridian.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.DemoData;

/// <summary>
/// Seeds and resets the demo portfolio as the application role. Each operation runs in one transaction holding a
/// PostgreSQL advisory lock, so replicas starting together can't seed twice, and readers never see a half-reset portfolio.
/// </summary>
internal sealed class DemoDataSeeder(MeridianDbContext db, TimeProvider clock) : IDemoData
{
    /// <summary>Arbitrary, stable key that identifies the demo-data lock.</summary>
    private const long AdvisoryLockKey = 0x4D45_5249_4449_414E; // "MERIDIAN"

    public Task EnsureSeededAsync(CancellationToken cancellationToken = default) =>
        InLockedTransactionAsync(async () =>
        {
            if (!await db.Clients.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                await InsertPortfolioAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken = default) =>
        InLockedTransactionAsync(async () =>
        {
            // Children first. The audit trail is deliberately untouched (and the app role couldn't delete it anyway).
            await db.Transactions.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Positions.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Accounts.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Securities.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await db.Clients.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await InsertPortfolioAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public async Task<DateTimeOffset?> LastResetAtAsync(CancellationToken cancellationToken = default) =>
        (await db.DemoDataState.AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false))?.LastResetAt;

    private async Task InsertPortfolioAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var data = DemoPortfolio.Generate(DateOnly.FromDateTime(now.UtcDateTime));

        db.Clients.AddRange(data.Clients);
        db.Securities.AddRange(data.Securities);
        db.Accounts.AddRange(data.Accounts);
        db.Transactions.AddRange(data.Transactions);

        var state = await db.DemoDataState.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            db.DemoDataState.Add(new DemoDataState { LastResetAt = now });
        }
        else
        {
            state.LastResetAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.ChangeTracker.Clear();
    }

    private async Task InLockedTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken).ConfigureAwait(false);
        await work().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
