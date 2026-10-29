using System.Collections.Concurrent;
using Meridian.Api.Auth;
using Meridian.Application.Accounts;
using Meridian.Application.Auditing;
using Meridian.Application.Quotes;
using Meridian.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Meridian.Api.Realtime;

/// <summary>
/// Live (simulated) prices for the account a client is viewing. The server values each holding and sends the results,
/// so the browser never multiplies money. Cookie-authenticated; SameSite=Strict keeps other sites from opening it.
/// </summary>
[Authorize]
internal sealed class QuotesHub(AccountQueries accounts, QuoteBoard board, ISecurityCatalog catalog, LiveSubscriptions subscriptions, IAuditTrail audit)
    : Hub
{
    public const string Path = "/hubs/quotes";
    public const string PositionsMethod = "positions";

    /// <summary>Starts sending live values for one of the caller's accounts (replacing any previous one).</summary>
    /// <returns>False when the account is missing or not the caller's (indistinguishable to the caller).</returns>
    public async Task<bool> WatchAccount(Guid accountId)
    {
        var user = Context.User!;
        var lookup = await accounts.GetAccountAsync(user.ClientIdOf(), accountId, Context.ConnectionAborted).ConfigureAwait(false);
        if (lookup.Status != AccountLookupStatus.Found)
        {
            if (lookup.Status == AccountLookupStatus.BelongsToAnotherClient)
            {
                await audit.RecordAsync(new AuditRecord($"user:{user.UserId()}", "AccessDenied", $"account:{accountId}"), Context.ConnectionAborted)
                    .ConfigureAwait(false);
            }

            return false;
        }

        if (!board.IsLoaded)
        {
            board.Load(await catalog.ListAsync(Context.ConnectionAborted).ConfigureAwait(false));
        }

        var watched = new WatchedAccount(lookup.Account!.Summary.Cash, [.. lookup.Account.Holdings.Select(h => new LiveHolding(h.Symbol, h.Quantity, h.CostBasis))]);
        subscriptions.Watch(Context.ConnectionId, watched);
        await Clients.Caller.SendAsync(PositionsMethod, LiveUpdate.From(board.ValueAccount(watched.Cash, watched.Holdings)), Context.ConnectionAborted)
            .ConfigureAwait(false);
        return true;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Forget(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Which holdings each connection is watching. In memory, per replica: see the README (live quotes) for scale-out.</summary>
internal sealed class LiveSubscriptions
{
    private readonly ConcurrentDictionary<string, WatchedAccount> _byConnection = new(StringComparer.Ordinal);

    public bool IsEmpty => _byConnection.IsEmpty;

    public void Watch(string connectionId, WatchedAccount account) => _byConnection[connectionId] = account;

    public void Forget(string connectionId) => _byConnection.TryRemove(connectionId, out _);

    public IEnumerable<KeyValuePair<string, WatchedAccount>> All => _byConnection;
}

internal sealed record WatchedAccount(Money Cash, IReadOnlyList<LiveHolding> Holdings);

/// <summary>Moves prices and pushes re-valued holdings to watchers. Idle (no ticks, no work) when nobody is watching.</summary>
internal sealed partial class QuoteTicker(QuoteBoard board, LiveSubscriptions subscriptions, IHubContext<QuotesHub> hub, IConfiguration configuration,
    ILogger<QuoteTicker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(configuration.GetValue("Quotes:IntervalMilliseconds", 3_000));
        var random = new Random();
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (subscriptions.IsEmpty || !board.IsLoaded)
            {
                continue;
            }

            board.Tick(random);
            foreach (var (connectionId, account) in subscriptions.All)
            {
                try
                {
                    var update = LiveUpdate.From(board.ValueAccount(account.Cash, account.Holdings));
                    await hub.Clients.Client(connectionId).SendAsync(QuotesHub.PositionsMethod, update, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSendFailed(logger, ex, connectionId);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't send live quotes to connection {ConnectionId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string connectionId);
}

internal sealed record LivePositionMessage(
    string Symbol, MoneyMessage Price, MoneyMessage MarketValue, MoneyMessage DayChange, MoneyMessage UnrealizedGain, decimal? UnrealizedGainPercent);

internal sealed record MoneyMessage(decimal Amount, string Currency)
{
    public static MoneyMessage From(Money money) => new(money.Amount, money.Currency);
}

/// <summary>What the browser receives on each tick: every figure already computed on the server.</summary>
internal sealed record LiveUpdate(DateTimeOffset AsOf, MoneyMessage TotalValue, MoneyMessage DayChange, IReadOnlyList<LivePositionMessage> Positions)
{
    public static LiveUpdate From(LiveValuation valuation) =>
        new(DateTimeOffset.UtcNow, MoneyMessage.From(valuation.TotalValue), MoneyMessage.From(valuation.DayChange),
            [.. valuation.Positions.Select(p => new LivePositionMessage(p.Symbol, MoneyMessage.From(p.Price), MoneyMessage.From(p.MarketValue),
                MoneyMessage.From(p.DayChange), MoneyMessage.From(p.UnrealizedGain), p.UnrealizedGainPercent))]);
}
