using Meridian.Domain;

namespace Meridian.Application.Accounts;

/// <summary>The columns positions can be sorted by. An allow-list: hosts map request values onto it, never onto property names.</summary>
public enum PositionSortField
{
    Symbol,
    Name,
    Quantity,
    Price,
    MarketValue,
    UnrealizedGain,
    DayChange,
}

public enum SortOrder
{
    Ascending,
    Descending,
}

/// <param name="Page">1-based.</param>
/// <param name="Filter">Case-insensitive match on symbol or name; null or blank matches everything.</param>
public sealed record PositionsQuery(
    int Page = 1,
    int PageSize = PositionsQuery.DefaultPageSize,
    PositionSortField SortBy = PositionSortField.MarketValue,
    SortOrder Order = SortOrder.Descending,
    string? Filter = null)
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int MaxFilterLength = 50;
}

public sealed record PositionView(
    string Symbol,
    string Name,
    AssetClass AssetClass,
    decimal Quantity,
    Money Price,
    Money MarketValue,
    Money CostBasis,
    Money UnrealizedGain,
    decimal? UnrealizedGainPercent,
    Money DayChange);

public sealed record PositionsPage(IReadOnlyList<PositionView> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

public sealed record PositionsLookup(AccountLookupStatus Status, PositionsPage? Positions);
