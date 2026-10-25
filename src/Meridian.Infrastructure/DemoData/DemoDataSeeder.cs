using Meridian.Application.DemoData;
using Meridian.Infrastructure.Identity;
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

    /// <summary>Seeds when there's no demo data, and upgrades data written by an older <see cref="DemoPortfolio.Version"/>.</summary>
    public Task EnsureSeededAsync(CancellationToken cancellationToken = default) =>
        InLockedTransactionAsync(async () =>
        {
            var demoClientIds = Generate().Clients.Select(c => c.Id).ToList();
            var seeded = await db.Clients.AnyAsync(c => demoClientIds.Contains(c.Id), cancellationToken).ConfigureAwait(false);
            var version = (await db.DemoDataState.AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false))?.DatasetVersion;

            if (!seeded)
            {
                await InsertPortfolioAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (version != DemoPortfolio.Version)
            {
                await ReplaceDemoDataAsync(cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <summary>
    /// Replaces the demo personas' data only. Registered users, their clients and the audit trail are untouched
    /// (the app role couldn't delete audit events anyway).
    /// </summary>
    public Task ResetAsync(CancellationToken cancellationToken = default) =>
        InLockedTransactionAsync(() => ReplaceDemoDataAsync(cancellationToken), cancellationToken);

    private async Task ReplaceDemoDataAsync(CancellationToken cancellationToken)
    {
        var demoClientIds = Generate().Clients.Select(c => c.Id).ToList();
        var demoAccounts = db.Accounts.Where(a => demoClientIds.Contains(a.ClientId)).Select(a => a.Id);

        // Children first.
        await db.Transactions.Where(t => demoAccounts.Contains(t.AccountId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Positions.Where(p => demoAccounts.Contains(p.AccountId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Accounts.Where(a => demoClientIds.Contains(a.ClientId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Users.Where(u => u.IsDemo).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Clients.Where(c => demoClientIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        // Accounts created by end-to-end test runs (their users cascade with the client).
        await db.Clients.Where(c => c.Email.EndsWith(DemoPortfolio.EndToEndEmailDomain)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Securities.Where(s => !db.Positions.Any(p => p.SecuritySymbol == s.Symbol)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await InsertPortfolioAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> LastResetAtAsync(CancellationToken cancellationToken = default) =>
        (await db.DemoDataState.AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false))?.LastResetAt;

    private DemoDataset Generate() => DemoPortfolio.Generate(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

    private async Task InsertPortfolioAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var data = Generate();

        var existingSymbols = await db.Securities.Select(s => s.Symbol).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var security in data.Securities)
        {
            db.Entry(security).State = existingSymbols.Contains(security.Symbol) ? EntityState.Unchanged : EntityState.Added;
        }

        db.Clients.AddRange(data.Clients);
        db.Accounts.AddRange(data.Accounts);
        db.Transactions.AddRange(data.Transactions);
        db.Users.AddRange(data.Clients.Select(DemoUser));

        var state = await db.DemoDataState.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            db.DemoDataState.Add(new DemoDataState { LastResetAt = now, DatasetVersion = DemoPortfolio.Version });
        }
        else
        {
            state.LastResetAt = now;
            state.DatasetVersion = DemoPortfolio.Version;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        db.ChangeTracker.Clear();
    }

    /// <summary>A sign-in account for a demo persona: no password; signs in through the demo endpoint only.</summary>
    private static ApplicationUser DemoUser(Meridian.Domain.Client client) => new()
    {
        Id = client.Id,
        ClientId = client.Id,
        DisplayName = client.DisplayName,
        UserName = client.Email,
        NormalizedUserName = client.Email.ToUpperInvariant(),
        Email = client.Email,
        NormalizedEmail = client.Email.ToUpperInvariant(),
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        LockoutEnabled = true,
        IsDemo = true,
    };

    private async Task InLockedTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken).ConfigureAwait(false);
        await work().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
