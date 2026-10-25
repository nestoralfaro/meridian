using Meridian.Domain;

namespace Meridian.Application.Accounts;

/// <summary>Read-side use cases for a signed-in client's accounts. Every query is scoped to the client.</summary>
public sealed class AccountQueries(IAccountRepository accounts)
{
    private const string ReportingCurrency = "USD";

    public async Task<AccountsOverview> GetOverviewAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var owned = await accounts.ListForClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        var summaries = owned.OrderBy(a => a.Type).ThenBy(a => a.Name, StringComparer.Ordinal).Select(Summarize).ToList();

        var total = summaries.Select(a => a.TotalValue).Sum(ReportingCurrency);
        var dayChange = summaries.Select(a => a.DayChange).Sum(ReportingCurrency);
        return new AccountsOverview(summaries, total, dayChange, PercentOf(dayChange, total));
    }

    public async Task<AccountLookup> GetAccountAsync(Guid clientId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new AccountLookup(AccountLookupStatus.NotFound, null);
        }

        if (account.ClientId != clientId)
        {
            return new AccountLookup(AccountLookupStatus.BelongsToAnotherClient, null);
        }

        var holdings = account.Positions
            .OrderByDescending(p => p.MarketValue.Amount)
            .Select(p => new HoldingView(p.Security.Symbol, p.Security.Name, p.Security.AssetClass, p.Quantity, p.Security.Price, p.MarketValue, p.DayChange))
            .ToList();
        return new AccountLookup(AccountLookupStatus.Found, new AccountDetail(Summarize(account), holdings));
    }

    private static AccountSummary Summarize(Account account) =>
        new(account.Id, account.Name, account.Type, account.MaskedNumber, account.TotalValue, account.Cash, account.DayChange, account.DayChangePercent);

    /// <summary>Day change as a percentage of the previous value, to two decimal places.</summary>
    private static decimal PercentOf(Money dayChange, Money total)
    {
        var previous = (total - dayChange).Amount;
        return previous == 0m ? 0m : Math.Round(dayChange.Amount / previous * 100m, 2, MidpointRounding.ToEven);
    }
}
