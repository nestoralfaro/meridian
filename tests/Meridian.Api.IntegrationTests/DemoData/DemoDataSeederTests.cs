using Meridian.Application.DemoData;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Domain;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Api.IntegrationTests.DemoData;

/// <summary>Seeding runs as the least-privilege application role against an isolated, migrated database.</summary>
public sealed class DemoDataSeederTests : IAsyncLifetime
{
    private MeridianApiFactory _factory = null!;

    public async ValueTask InitializeAsync() =>
        _factory = new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync());

    [Fact]
    public async Task SeedsTheDemoPortfolioIntoAnEmptyDatabase()
    {
        await UseDemoDataAsync(demo => demo.EnsureSeededAsync(TestContext.Current.CancellationToken));

        await UseDbAsync(async db =>
        {
            (await db.Clients.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
            var dana = await db.Clients.SingleAsync(c => c.Email == DemoPortfolio.DanaEmail, TestContext.Current.CancellationToken);
            var brokerage = await db.Accounts.Include(a => a.Positions).ThenInclude(p => p.Security)
                .SingleAsync(a => a.ClientId == dana.Id && a.Type == AccountType.IndividualBrokerage, TestContext.Current.CancellationToken);
            brokerage.TotalValue.ShouldBe(Money.Usd(12480.55m)); // round-trips through PostgreSQL exactly
            (await db.DemoDataState.SingleAsync(TestContext.Current.CancellationToken)).LastResetAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        });
    }

    [Fact]
    public async Task SeedingTwiceChangesNothing()
    {
        await UseDemoDataAsync(demo => demo.EnsureSeededAsync(TestContext.Current.CancellationToken));
        var before = await CountsAsync();

        await UseDemoDataAsync(demo => demo.EnsureSeededAsync(TestContext.Current.CancellationToken));

        (await CountsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task ConcurrentSeedingNeverDuplicatesData()
    {
        // Several replicas can start at the same time; an advisory lock serializes them.
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => UseDemoDataAsync(demo => demo.EnsureSeededAsync(TestContext.Current.CancellationToken))));

        await UseDbAsync(async db => (await db.Clients.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2));
    }

    [Fact]
    public async Task ResetRestoresThePortfolioAndLeavesTheAuditTrailAlone()
    {
        await UseDemoDataAsync(demo => demo.EnsureSeededAsync(TestContext.Current.CancellationToken));
        var expected = await CountsAsync();
        await UseDbAsync(async db =>
        {
            db.AuditEvents.Add(new AuditEvent { Actor = "user:dana", Action = "TransferCompleted", Subject = "account:x" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.Transactions.Take(3).ExecuteDeleteAsync(TestContext.Current.CancellationToken); // a demo user changed things
        });

        await UseDemoDataAsync(demo => demo.ResetAsync(TestContext.Current.CancellationToken));

        (await CountsAsync()).ShouldBe(expected);
        await UseDbAsync(async db => (await db.AuditEvents.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1));
    }

    private async Task<(int Clients, int Accounts, int Positions, int Transactions, int Securities)> CountsAsync()
    {
        (int, int, int, int, int) counts = default;
        await UseDbAsync(async db => counts = (
            await db.Clients.CountAsync(TestContext.Current.CancellationToken),
            await db.Accounts.CountAsync(TestContext.Current.CancellationToken),
            await db.Positions.CountAsync(TestContext.Current.CancellationToken),
            await db.Transactions.CountAsync(TestContext.Current.CancellationToken),
            await db.Securities.CountAsync(TestContext.Current.CancellationToken)));
        return counts;
    }

    private async Task UseDemoDataAsync(Func<IDemoData, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IDemoData>());
    }

    private async Task UseDbAsync(Func<MeridianDbContext, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<MeridianDbContext>());
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();
}
