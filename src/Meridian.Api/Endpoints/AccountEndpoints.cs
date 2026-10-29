using System.Security.Claims;
using Meridian.Api.Auth;
using Meridian.Application.Accounts;
using Meridian.Application.Auditing;
using Meridian.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Api.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/accounts").WithTags("Accounts").RequireAuthorization();

        accounts.MapGet("/", GetOverviewAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetAccountsOverview")
            .WithSummary("The signed-in client's accounts with balances, day change and totals.");

        accounts.MapGet("/{accountId:guid}", GetAccountAsync)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetAccount")
            .WithSummary("One of the signed-in client's accounts with its holdings. Another client's account is indistinguishable from a missing one.");

        accounts.MapGet("/{accountId:guid}/positions", GetPositionsAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetAccountPositions")
            .WithSummary("One page of an account's positions, filtered by symbol or name and sorted on the server.")
            .WithDescription($"sort: {string.Join(", ", SortFields.Keys)} (default marketValue). order: asc or desc (default desc). " +
                $"pageSize: 1–{PositionsQuery.MaxPageSize} (default {PositionsQuery.DefaultPageSize}). filter: up to {PositionsQuery.MaxFilterLength} characters.");

        return endpoints;
    }

    /// <summary>The only sort values accepted. Mapped explicitly so numeric or misspelt enum names can't slip through.</summary>
    private static readonly Dictionary<string, PositionSortField> SortFields = new(StringComparer.Ordinal)
    {
        ["symbol"] = PositionSortField.Symbol,
        ["name"] = PositionSortField.Name,
        ["quantity"] = PositionSortField.Quantity,
        ["price"] = PositionSortField.Price,
        ["marketValue"] = PositionSortField.MarketValue,
        ["unrealizedGain"] = PositionSortField.UnrealizedGain,
        ["dayChange"] = PositionSortField.DayChange,
    };

    private static async Task<Results<Ok<PositionsPageResponse>, ProblemHttpResult, ValidationProblem>> GetPositionsAsync(
        Guid accountId, ClaimsPrincipal user, AccountQueries queries, IAuditTrail audit, CancellationToken cancellationToken,
        int page = 1, int pageSize = PositionsQuery.DefaultPageSize, string sort = "marketValue", string order = "desc", string? filter = null)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (page < 1)
        {
            errors["page"] = ["Must be 1 or more."];
        }

        if (pageSize is < 1 or > PositionsQuery.MaxPageSize)
        {
            errors["pageSize"] = [$"Must be between 1 and {PositionsQuery.MaxPageSize}."];
        }

        if (!SortFields.TryGetValue(sort, out var sortBy))
        {
            errors["sort"] = [$"Must be one of: {string.Join(", ", SortFields.Keys)}."];
        }

        if (order is not ("asc" or "desc"))
        {
            errors["order"] = ["Must be asc or desc."];
        }

        if (filter?.Length > PositionsQuery.MaxFilterLength)
        {
            errors["filter"] = [$"Must be {PositionsQuery.MaxFilterLength} characters or fewer."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = new PositionsQuery(page, pageSize, sortBy, order == "asc" ? SortOrder.Ascending : SortOrder.Descending, filter);
        var lookup = await queries.GetPositionsAsync(user.ClientIdOf(), accountId, query, cancellationToken).ConfigureAwait(false);
        switch (lookup.Status)
        {
            case AccountLookupStatus.Found:
                var positions = lookup.Positions!;
                return TypedResults.Ok(new PositionsPageResponse(
                    [.. positions.Items.Select(p => new PositionResponse(p.Symbol, p.Name, p.AssetClass.ToString(), p.Quantity,
                        MoneyResponse.From(p.Price), MoneyResponse.From(p.MarketValue), MoneyResponse.From(p.CostBasis),
                        MoneyResponse.From(p.UnrealizedGain), p.UnrealizedGainPercent, MoneyResponse.From(p.DayChange)))],
                    positions.Page, positions.PageSize, positions.TotalCount, positions.TotalPages));

            case AccountLookupStatus.BelongsToAnotherClient:
                await audit.RecordAsync(new AuditRecord($"user:{user.UserId()}", "AccessDenied", $"account:{accountId}"), cancellationToken).ConfigureAwait(false);
                return NotFound();

            default:
                return NotFound();
        }
    }

    private static async Task<Ok<AccountsOverviewResponse>> GetOverviewAsync(ClaimsPrincipal user, AccountQueries queries, CancellationToken cancellationToken)
    {
        var overview = await queries.GetOverviewAsync(user.ClientIdOf(), cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new AccountsOverviewResponse(
            [.. overview.Accounts.Select(ToResponse)],
            MoneyResponse.From(overview.TotalValue),
            MoneyResponse.From(overview.DayChange),
            overview.DayChangePercent,
            [.. overview.Allocation.Select(s => new AllocationSliceResponse(s.AssetClass.ToString(), MoneyResponse.From(s.Value), s.Percent))]));
    }

    private static async Task<Results<Ok<AccountDetailResponse>, ProblemHttpResult>> GetAccountAsync(
        Guid accountId, ClaimsPrincipal user, AccountQueries queries, IAuditTrail audit, CancellationToken cancellationToken)
    {
        var lookup = await queries.GetAccountAsync(user.ClientIdOf(), accountId, cancellationToken).ConfigureAwait(false);
        switch (lookup.Status)
        {
            case AccountLookupStatus.Found:
                var account = lookup.Account!;
                return TypedResults.Ok(new AccountDetailResponse(
                    account.Summary.Id, account.Summary.Name, account.Summary.Type.ToString(), account.Summary.MaskedNumber,
                    MoneyResponse.From(account.Summary.TotalValue), MoneyResponse.From(account.Summary.Cash),
                    MoneyResponse.From(account.Summary.DayChange), account.Summary.DayChangePercent,
                    [.. account.Holdings.Select(h => new HoldingResponse(h.Symbol, h.Name, h.AssetClass.ToString(), h.Quantity,
                        MoneyResponse.From(h.Price), MoneyResponse.From(h.MarketValue), MoneyResponse.From(h.DayChange)))]));

            case AccountLookupStatus.BelongsToAnotherClient:
                await audit.RecordAsync(new AuditRecord($"user:{user.UserId()}", "AccessDenied", $"account:{accountId}"), cancellationToken).ConfigureAwait(false);
                return NotFound();

            default:
                return NotFound();
        }
    }

    /// <summary>Identical for missing accounts and other clients' accounts, so IDs can't be probed.</summary>
    private static ProblemHttpResult NotFound() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Account not found.");

    private static AccountSummaryResponse ToResponse(AccountSummary a) =>
        new(a.Id, a.Name, a.Type.ToString(), a.MaskedNumber, MoneyResponse.From(a.TotalValue), MoneyResponse.From(a.Cash), MoneyResponse.From(a.DayChange), a.DayChangePercent);
}

/// <summary>Exact decimal amount plus ISO 4217 currency. The SPA formats it; it never does arithmetic on it.</summary>
internal sealed record MoneyResponse(decimal Amount, string Currency)
{
    public static MoneyResponse From(Money money) => new(money.Amount, money.Currency);
}

internal sealed record AccountSummaryResponse(
    Guid Id, string Name, string Type, string MaskedNumber, MoneyResponse TotalValue, MoneyResponse Cash, MoneyResponse DayChange, decimal DayChangePercent);

/// <param name="Percent">Share of the total value; the slices' percentages add up to exactly 100.</param>
internal sealed record AllocationSliceResponse(string AssetClass, MoneyResponse Value, decimal Percent);

internal sealed record AccountsOverviewResponse(
    IReadOnlyList<AccountSummaryResponse> Accounts,
    MoneyResponse TotalValue,
    MoneyResponse DayChange,
    decimal DayChangePercent,
    IReadOnlyList<AllocationSliceResponse> Allocation);

internal sealed record HoldingResponse(
    string Symbol, string Name, string AssetClass, decimal Quantity, MoneyResponse Price, MoneyResponse MarketValue, MoneyResponse DayChange);

internal sealed record AccountDetailResponse(
    Guid Id, string Name, string Type, string MaskedNumber, MoneyResponse TotalValue, MoneyResponse Cash, MoneyResponse DayChange,
    decimal DayChangePercent, IReadOnlyList<HoldingResponse> Holdings);

/// <param name="UnrealizedGainPercent">Gain as a percentage of cost basis; null when the cost basis is zero.</param>
internal sealed record PositionResponse(
    string Symbol, string Name, string AssetClass, decimal Quantity, MoneyResponse Price, MoneyResponse MarketValue, MoneyResponse CostBasis,
    MoneyResponse UnrealizedGain, decimal? UnrealizedGainPercent, MoneyResponse DayChange);

/// <param name="TotalCount">Positions matching the filter, across all pages.</param>
internal sealed record PositionsPageResponse(IReadOnlyList<PositionResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);
