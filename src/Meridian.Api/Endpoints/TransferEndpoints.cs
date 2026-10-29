using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Meridian.Api.Auth;
using Meridian.Application.Auditing;
using Meridian.Application.Transfers;
using Meridian.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Endpoints;

internal static partial class TransferEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/transfers", TransferAsync)
            .RequireAuthorization()
            .AddEndpointFilter<ValidateAntiforgery>()
            .WithTags("Transfers")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithName("CreateTransfer")
            .WithSummary("Moves cash between two of the signed-in client's accounts.")
            .WithDescription(
                $"Send a unique {IdempotencyKeyHeader} header (1–{Transfer.MaxIdempotencyKeyLength} letters, digits, '-' or '_') per intended transfer. " +
                $"201: completed. 200 with {ReplayedHeader}: true: a retry of a completed transfer; nothing moved again. " +
                "409: the source account changed since fromAccountVersion was read; review again. " +
                "422: insufficient cash, or the key was already used for a different transfer. " +
                "404: an account is missing or not yours (indistinguishable).");
        return endpoints;
    }

    private static async Task<Results<Created<TransferResponse>, Ok<TransferResponse>, ProblemHttpResult, ValidationProblem>> TransferAsync(
        CreateTransferRequest body, [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        ClaimsPrincipal user, TransferCommands commands, IAuditTrail audit, HttpContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || !IdempotencyKeyFormat().IsMatch(idempotencyKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [IdempotencyKeyHeader] = [$"Required: 1–{Transfer.MaxIdempotencyKeyLength} letters, digits, '-' or '_'."],
            });
        }

        var actor = $"user:{user.UserId()}";
        var request = new TransferRequest(user.ClientIdOf(), body.FromAccountId, body.ToAccountId, Money.Usd(body.Amount), idempotencyKey, body.FromAccountVersion);
        var outcome = await commands.TransferAsync(request, actor, cancellationToken).ConfigureAwait(false);

        switch (outcome.Status)
        {
            case TransferStatus.Completed:
                return TypedResults.Created((string?)null, ToResponse(outcome.Transfer!));

            case TransferStatus.Replayed:
                context.Response.Headers[ReplayedHeader] = "true";
                return TypedResults.Ok(ToResponse(outcome.Transfer!));

            case TransferStatus.AccountNotFound:
                if (outcome.ForeignAccountId is { } foreign)
                {
                    await audit.RecordAsync(new AuditRecord(actor, "AccessDenied", $"account:{foreign}"), cancellationToken).ConfigureAwait(false);
                }

                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Account not found.");

            case TransferStatus.SameAccount:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["toAccountId"] = ["Choose a different account to transfer to."] });

            case TransferStatus.InvalidAmount:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amount"] = [$"Enter an amount between $0.01 and {TransferCommands.MaxAmount.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}, in whole cents."],
                });

            case TransferStatus.InsufficientCash:
                var available = outcome.AvailableCash!.Value;
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Not enough cash.",
                    detail: $"You have {available.Amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))} available to transfer from this account.",
                    extensions: new Dictionary<string, object?> { ["availableCash"] = MoneyResponse.From(available) });

            case TransferStatus.KeyReused:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Idempotency key already used.",
                    detail: $"This {IdempotencyKeyHeader} was used for a different transfer. Use a new key for a new transfer.");

            case TransferStatus.Conflict:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Your account changed.",
                    detail: "The balance changed since you reviewed this transfer. Review it again before confirming.");

            default:
                throw new InvalidOperationException($"Unhandled transfer status {outcome.Status}.");
        }
    }

    private static TransferResponse ToResponse(TransferView t) =>
        new(t.Id, t.FromAccountId, t.ToAccountId, MoneyResponse.From(t.Amount), t.RequestedAt);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdempotencyKeyFormat();
}

/// <param name="Amount">US dollars, in whole cents.</param>
/// <param name="FromAccountVersion">The source account's <c>version</c> from when the transfer was reviewed.</param>
internal sealed record CreateTransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, uint FromAccountVersion);

internal sealed record TransferResponse(Guid Id, Guid FromAccountId, Guid ToAccountId, MoneyResponse Amount, DateTimeOffset RequestedAt);
