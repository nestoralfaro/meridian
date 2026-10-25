using System.Security.Claims;
using Meridian.Application.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Api.Auth;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Auth");

        auth.MapGet("/antiforgery", (HttpContext context) =>
            {
                AuthSetup.IssueAntiforgeryToken(context);
                return TypedResults.NoContent();
            })
            .WithName("GetAntiforgeryToken")
            .WithSummary("Issues an antiforgery token (XSRF-TOKEN cookie) to send as X-XSRF-TOKEN on state-changing requests.");

        auth.MapGet("/me", (ClaimsPrincipal user) =>
                TypedResults.Ok(new MeResponse(user.FindFirstValue(ClaimTypes.Name)!, user.FindFirstValue(ClaimTypes.Email)!, user.IsDemo())))
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user. 401 when signed out.");

        auth.MapPost("/demo-sign-in", DemoSignInAsync)
            .AddEndpointFilter<ValidateAntiforgery>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("DemoSignIn")
            .WithSummary("Signs in as a ready-made demo persona (no credentials).");

        auth.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .AddEndpointFilter<ValidateAntiforgery>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithName("ChangePassword")
            .WithSummary("Changes the signed-in user's password. Demo personas can't change credentials.");

        return endpoints;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DemoSignInAsync(
        DemoSignInRequest request, IIdentityService identity, HttpContext context, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DemoPersona>(request.Persona, ignoreCase: true, out var persona) || !Enum.IsDefined(persona))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown demo persona.");
        }

        var user = await identity.FindDemoPersonaAsync(persona, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Demo data isn't available yet.");
        }

        await AuthSetup.SignInAsync(context, user).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ChangePasswordAsync(
        ChangePasswordRequest request, IIdentityService identity, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var result = await identity.ChangePasswordAsync(user.UserId(), request.CurrentPassword, request.NewPassword, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            ChangePasswordOutcome.Changed => TypedResults.NoContent(),
            ChangePasswordOutcome.DemoAccount => TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden, title: "Not allowed for demo accounts.", detail: "Demo accounts can't change their password"),
            ChangePasswordOutcome.IncorrectCurrentPassword => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest, title: "The current password is incorrect."),
            ChangePasswordOutcome.NewPasswordRejected => TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["newPassword"] = [.. result.Errors] }),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found."),
        };
    }
}

internal sealed record MeResponse(string DisplayName, string Email, bool IsDemo);

internal sealed record DemoSignInRequest(string Persona);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
