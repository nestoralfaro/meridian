using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class LiveQuotesSteps(ApiScenario api) : IAsyncDisposable
{
    private static readonly Uri BaseAddress = new("https://localhost");
    private readonly CookieContainer _cookies = new();
    private readonly Channel<LiveUpdate> _updates = Channel.CreateUnbounded<LiveUpdate>();
    private HubConnection? _connection;
    private bool _watching;
    private Exception? _connectFailure;

    [Given("Dana is connected to live quotes")]
    public async Task GivenDanaIsConnected()
    {
        using var http = new HttpClient(Handler()) { BaseAddress = BaseAddress };
        (await http.GetAsync(new Uri("/api/v1/auth/antiforgery", UriKind.Relative))).EnsureSuccessStatusCode();
        var token = _cookies.GetCookies(BaseAddress)["XSRF-TOKEN"]!.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/demo-sign-in") { Content = JsonContent.Create(new { persona = "dana" }) };
        request.Headers.Add(ApiScenario.XsrfHeader, Uri.UnescapeDataString(token));
        (await http.SendAsync(request)).EnsureSuccessStatusCode();

        _connection = Build();
        _connection.On<LiveUpdate>("positions", update => _updates.Writer.TryWrite(update));
        await _connection.StartAsync();
    }

    [When(@"she watches her ""(.*)""")]
    public async Task WhenSheWatchesHerAccount(string name) =>
        _watching = await _connection!.InvokeAsync<bool>("WatchAccount", await AccountIdAsync(DemoPortfolio.DanaEmail, name));

    [When(@"she watches Raj's ""(.*)""")]
    public async Task WhenSheWatchesRajsAccount(string name) =>
        _watching = await _connection!.InvokeAsync<bool>("WatchAccount", await AccountIdAsync(DemoPortfolio.RajEmail, name));

    [When("an anonymous visitor connects to live quotes")]
    public async Task WhenAnAnonymousVisitorConnects()
    {
        api.Start(new MeridianApiFactory(TestDatabase.Unreachable));
        _connection = Build();
        _connectFailure = await Record.ExceptionAsync(() => _connection.StartAsync());
    }

    [Then("she is told the account is being watched")]
    public void ThenTheAccountIsWatched() => _watching.ShouldBeTrue();

    [Then("she is told the account can't be watched")]
    public void ThenTheAccountIsNotWatched() => _watching.ShouldBeFalse();

    [Then(@"she receives live values for ""(.*)"" valued by the server")]
    public async Task ThenSheReceivesLiveValues(string symbols)
    {
        var update = await NextUpdateAsync(TimeSpan.FromSeconds(5));
        update.ShouldNotBeNull();
        update.Positions.Select(p => p.Symbol).ShouldBe(symbols.Split(", "), ignoreOrder: true);
        update.Positions.ShouldAllBe(p => p.MarketValue.Amount > 0 && p.MarketValue.Currency == "USD");
        update.TotalValue.Amount.ShouldBe(1381.05m + update.Positions.Sum(p => p.MarketValue.Amount)); // cash + holdings
    }

    [Then(@"another update arrives within (\d+) seconds")]
    public async Task ThenAnotherUpdateArrives(int seconds) =>
        (await NextUpdateAsync(TimeSpan.FromSeconds(seconds))).ShouldNotBeNull();

    [Then("she receives no live values")]
    public async Task ThenSheReceivesNothing() =>
        (await NextUpdateAsync(TimeSpan.FromSeconds(1))).ShouldBeNull();

    [Then("the connection is refused")]
    public void ThenTheConnectionIsRefused()
    {
        var failure = _connectFailure.ShouldBeOfType<HttpRequestException>();
        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private HubConnection Build() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(BaseAddress, "/hubs/quotes"), options =>
            {
                options.Transports = HttpTransportType.LongPolling; // TestServer has no real sockets
                options.HttpMessageHandlerFactory = _ => Handler();
            })
            .Build();

    private CookieContainerHandler Handler() => new(_cookies) { InnerHandler = api.Factory.Server.CreateHandler() };

    private async Task<LiveUpdate?> NextUpdateAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await _updates.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<Guid> AccountIdAsync(string email, string name)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
        return await db.Accounts.Where(a => db.Clients.Where(c => c.Email == email).Select(c => c.Id).Contains(a.ClientId) && a.Name == name)
            .Select(a => a.Id).SingleAsync();
    }

    public sealed record Money(decimal Amount, string Currency);

    public sealed record LivePosition(string Symbol, Money Price, Money MarketValue, Money DayChange, Money UnrealizedGain);

    public sealed record LiveUpdate(DateTimeOffset AsOf, Money TotalValue, Money DayChange, IReadOnlyList<LivePosition> Positions);
}
