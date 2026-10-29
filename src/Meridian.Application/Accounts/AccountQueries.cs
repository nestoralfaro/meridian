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
        return new AccountsOverview(summaries, total, dayChange, PercentOf(dayChange, total), Allocate(owned, total));
    }

    /// <summary>Value by asset class (uninvested cash counts as Cash), largest first.</summary>
    private static List<AllocationSlice> Allocate(IReadOnlyList<Account> owned, Money total)
    {
        if (total.Amount == 0m)
        {
            return [];
        }

        var byClass = owned
            .SelectMany(a => a.Positions.Select(p => (AssetClass: p.Security.AssetClass, Value: p.MarketValue)).Append((AssetClass: AssetClass.Cash, Value: a.Cash)))
            .GroupBy(x => x.AssetClass)
            .Select(g => (AssetClass: g.Key, Value: g.Select(x => x.Value).Sum(ReportingCurrency)))
            .Where(x => x.Value.Amount != 0m)
            .OrderByDescending(x => x.Value.Amount)
            .ToList();

        var percents = LargestRemainderPercents([.. byClass.Select(x => x.Value.Amount)], total.Amount);
        return [.. byClass.Select((x, i) => new AllocationSlice(x.AssetClass, x.Value, percents[i]))];
    }

    /// <summary>
    /// Percentages to two decimals that add up to exactly 100: floor each share, then give the missing hundredths to the
    /// shares with the largest remainders (Hamilton's method). Independent rounding can total 99.99 or 100.01.
    /// </summary>
    private static decimal[] LargestRemainderPercents(IReadOnlyList<decimal> values, decimal total)
    {
        var raw = values.Select(v => v / total * 10_000m).ToArray(); // in hundredths of a percent
        var floors = raw.Select(Math.Floor).ToArray();
        var missing = (int)(10_000m - floors.Sum());
        foreach (var index in Enumerable.Range(0, raw.Length).OrderByDescending(i => raw[i] - floors[i]).Take(missing))
        {
            floors[index] += 1m;
        }

        return [.. floors.Select(f => f / 100m)];
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

    /// <summary>
    /// One page of an account's positions, filtered and sorted. Ties are broken by symbol so pages never overlap or skip.
    /// Paging happens here rather than in SQL: the account aggregate is loaded anyway (ownership check, valuation needs
    /// prices), and positions per account are bounded by the number of securities.
    /// </summary>
    public async Task<PositionsLookup> GetPositionsAsync(Guid clientId, Guid accountId, PositionsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, PositionsQuery.MaxPageSize);

        var account = await accounts.FindAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null || account.ClientId != clientId)
        {
            return new PositionsLookup(account is null ? AccountLookupStatus.NotFound : AccountLookupStatus.BelongsToAnotherClient, null);
        }

        var filter = query.Filter?.Trim();
        var matching = account.Positions
            .Where(p => string.IsNullOrEmpty(filter)
                || p.Security.Symbol.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || p.Security.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(p => new PositionView(p.Security.Symbol, p.Security.Name, p.Security.AssetClass, p.Quantity, p.Security.Price,
                p.MarketValue, p.CostBasis, p.UnrealizedGain, p.UnrealizedGainPercent, p.DayChange))
            .ToList();

        var sorted = Sort(matching, query.SortBy, query.Order).ThenBy(p => p.Symbol, StringComparer.Ordinal);
        var page = sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new PositionsLookup(AccountLookupStatus.Found, new PositionsPage(page, query.Page, query.PageSize, matching.Count));
    }

    private static IOrderedEnumerable<PositionView> Sort(IEnumerable<PositionView> positions, PositionSortField field, SortOrder order)
    {
        return field switch
        {
            PositionSortField.Symbol => By(p => p.Symbol, StringComparer.Ordinal),
            PositionSortField.Name => By(p => p.Name, StringComparer.OrdinalIgnoreCase),
            PositionSortField.Quantity => By(p => p.Quantity, Comparer<decimal>.Default),
            PositionSortField.Price => By(p => p.Price.Amount, Comparer<decimal>.Default),
            PositionSortField.MarketValue => By(p => p.MarketValue.Amount, Comparer<decimal>.Default),
            PositionSortField.UnrealizedGain => By(p => p.UnrealizedGain.Amount, Comparer<decimal>.Default),
            PositionSortField.DayChange => By(p => p.DayChange.Amount, Comparer<decimal>.Default),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown sort field."),
        };

        IOrderedEnumerable<PositionView> By<TKey>(Func<PositionView, TKey> key, IComparer<TKey> comparer) =>
            order == SortOrder.Descending ? positions.OrderByDescending(key, comparer) : positions.OrderBy(key, comparer);
    }

    public const int MaxActivityItems = 50;

    /// <summary>Newest first; <paramref name="limit"/> is clamped to 1–<see cref="MaxActivityItems"/>.</summary>
    public async Task<IReadOnlyList<ActivityView>> GetRecentActivityAsync(Guid clientId, int limit, CancellationToken cancellationToken = default)
    {
        var recent = await accounts.RecentTransactionsAsync(clientId, Math.Clamp(limit, 1, MaxActivityItems), cancellationToken).ConfigureAwait(false);
        return [.. recent.Select(r => new ActivityView(r.Transaction.Id, r.Transaction.Date, r.AccountName, r.Transaction.Description, r.Transaction.Type, r.Transaction.Amount))];
    }

    private static AccountSummary Summarize(Account account) =>
        new(account.Id, account.Name, account.Type, account.MaskedNumber, account.TotalValue, account.Cash, account.DayChange, account.DayChangePercent,
            account.Version);

    /// <summary>Day change as a percentage of the previous value, to two decimal places.</summary>
    private static decimal PercentOf(Money dayChange, Money total)
    {
        var previous = (total - dayChange).Amount;
        return previous == 0m ? 0m : Math.Round(dayChange.Amount / previous * 100m, 2, MidpointRounding.ToEven);
    }
}
