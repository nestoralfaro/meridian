using Meridian.Domain;

namespace Meridian.Application.Accounts;

public sealed record AccountSummary(
    Guid Id,
    string Name,
    AccountType Type,
    string MaskedNumber,
    Money TotalValue,
    Money Cash,
    Money DayChange,
    decimal DayChangePercent,
    uint Version);

/// <summary>One asset class's share of the client's total value. Percentages across slices add up to exactly 100.</summary>
public sealed record AllocationSlice(AssetClass AssetClass, Money Value, decimal Percent);

public sealed record AccountsOverview(
    IReadOnlyList<AccountSummary> Accounts,
    Money TotalValue,
    Money DayChange,
    decimal DayChangePercent,
    IReadOnlyList<AllocationSlice> Allocation);

public sealed record HoldingView(string Symbol, string Name, AssetClass AssetClass, decimal Quantity, Money Price, Money MarketValue, Money DayChange);

public sealed record AccountDetail(AccountSummary Summary, IReadOnlyList<HoldingView> Holdings);

public enum AccountLookupStatus
{
    Found,
    NotFound,

    /// <summary>Exists but belongs to someone else. Hosts must answer exactly as for <see cref="NotFound"/> (no enumeration) and audit it.</summary>
    BelongsToAnotherClient,
}

public sealed record AccountLookup(AccountLookupStatus Status, AccountDetail? Account);

public sealed record ActivityView(Guid Id, DateOnly Date, string AccountName, string Description, TransactionType Type, Money Amount);
