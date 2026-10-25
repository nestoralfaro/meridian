using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Api.IntegrationTests.DemoData;

/// <summary>
/// The app keeps its own demo data fresh: seeded on first start, reset once it's older than a day.
/// This works with scale-to-zero (the check runs whenever the app wakes) and needs no external scheduler or secrets.
/// </summary>
public sealed class DemoDataMaintenanceTests
{
    [Fact]
    public async Task AnEmptyDatabaseIsSeededWhenTheAppStarts()
    {
        await using var factory = new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync())
            .WithSetting("Demo:Enabled", "true");
        _ = factory.CreateClient(); // starts the host

        await EventuallyAsync(factory, async db => await db.Clients.CountAsync() == 2);
    }

    [Fact]
    public async Task StaleDemoDataIsResetWhenTheAppStarts()
    {
        var connectionString = await TestDatabase.CreateIsolatedDatabaseAsync();
        await using (var seeding = new MeridianApiFactory(connectionString).WithSetting("Demo:Enabled", "true"))
        {
            _ = seeding.CreateClient();
            await EventuallyAsync(seeding, async db => await db.Clients.CountAsync() == 2);
            await UseDbAsync(seeding, async db =>
            {
                await db.Transactions.ExecuteDeleteAsync(); // a visitor's changes…
                await db.DemoDataState.ExecuteUpdateAsync(s => s.SetProperty(x => x.LastResetAt, DateTimeOffset.UtcNow.AddDays(-2))); // …two days ago
            });
        }

        await using var restarted = new MeridianApiFactory(connectionString).WithSetting("Demo:Enabled", "true");
        _ = restarted.CreateClient();

        await EventuallyAsync(restarted, async db => await db.Transactions.AnyAsync());
    }

    [Fact]
    public async Task DisabledByDefaultSoTestsAndRealDeploymentsOptIn()
    {
        await using var factory = new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync());
        _ = factory.CreateClient();
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        await UseDbAsync(factory, async db => (await db.Clients.CountAsync()).ShouldBe(0));
    }

    private static async Task EventuallyAsync(MeridianApiFactory factory, Func<MeridianDbContext, Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var done = false;
            await UseDbAsync(factory, async db => done = await condition(db));
            if (done)
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new ShouldAssertException("Condition not met within 10 seconds.");
    }

    private static async Task UseDbAsync(MeridianApiFactory factory, Func<MeridianDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<MeridianDbContext>());
    }
}
