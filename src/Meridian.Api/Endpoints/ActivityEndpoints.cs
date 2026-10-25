using System.Security.Claims;
using Meridian.Api.Auth;
using Meridian.Application.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Api.Endpoints;

internal static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/activity", GetActivityAsync)
            .RequireAuthorization()
            .WithTags("Accounts")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetRecentActivity")
            .WithSummary("The signed-in client's most recent transactions across their accounts, newest first (limit 1–50, default 5).");
        return endpoints;
    }

    private static async Task<Ok<ActivityResponse>> GetActivityAsync(ClaimsPrincipal user, AccountQueries queries, CancellationToken cancellationToken, int limit = 5)
    {
        var activity = await queries.GetRecentActivityAsync(user.ClientIdOf(), limit, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new ActivityResponse([.. activity.Select(a =>
            new ActivityItemResponse(a.Id, a.Date, a.AccountName, a.Description, a.Type.ToString(), MoneyResponse.From(a.Amount)))]));
    }
}

/// <param name="Amount">Positive adds cash to the account; negative removes it.</param>
internal sealed record ActivityItemResponse(Guid Id, DateOnly Date, string AccountName, string Description, string Type, MoneyResponse Amount);

internal sealed record ActivityResponse(IReadOnlyList<ActivityItemResponse> Items);
