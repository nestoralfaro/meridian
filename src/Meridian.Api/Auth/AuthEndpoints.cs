using System.Security.Claims;
using Meridian.Application.Auditing;
using Meridian.Application.Identity;
using Microsoft.AspNetCore.Authentication;
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

        auth.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidateAntiforgery>()
            .RequireRateLimiting(AuthSetup.SignInRateLimit)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("Register")
            .WithSummary("Creates an account and signs in. Passwords: at least 12 characters, not a common password.");

        auth.MapPost("/sign-in", SignInAsync)
            .AddEndpointFilter<ValidateAntiforgery>()
            .RequireRateLimiting(AuthSetup.SignInRateLimit)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SignIn")
            .WithSummary("Signs in with email and password. Every failure gets the same message (no account enumeration).");

        auth.MapPost("/sign-out", SignOutAsync)
            .AddEndpointFilter<ValidateAntiforgery>()
            .WithName("SignOut")
            .WithSummary("Ends the session.");

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

    /// <summary>One message for every failure: wrong password, unknown email, locked out, or demo account.</summary>
    internal const string SignInFailedDetail = "Sign-in failed. Check your email and password, or try again later.";

    private static async Task<Results<NoContent, ValidationProblem>> RegisterAsync(
        RegisterRequest request, IIdentityService identity, IAuditTrail audit, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await identity.RegisterAsync(request.DisplayName, request.Email, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(result.Errors);
        }

        var user = result.User!;
        await AuthSetup.SignInAsync(context, user).ConfigureAwait(false);
        await audit.RecordAsync(new AuditRecord($"user:{user.UserId}", "Registered", $"user:{user.UserId}"), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SignInAsync(
        SignInRequest request, IIdentityService identity, IAuditTrail audit, HttpContext context, CancellationToken cancellationToken)
    {
        var attempt = await identity.VerifyPasswordAsync(request.Email, request.Password, cancellationToken).ConfigureAwait(false);
        if (attempt is { Outcome: SignInOutcome.Succeeded, User: { } user })
        {
            await AuthSetup.SignInAsync(context, user).ConfigureAwait(false);
            await audit.RecordAsync(new AuditRecord($"user:{user.UserId}", "SignedIn", $"user:{user.UserId}"), cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }

        var action = attempt.Outcome == SignInOutcome.LockedOut ? "SignInLockedOut" : "SignInFailed";
        await audit.RecordAsync(new AuditRecord("anonymous", action, "sign-in"), cancellationToken).ConfigureAwait(false);
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed.", detail: SignInFailedDetail);
    }

    private static async Task<NoContent> SignOutAsync(ClaimsPrincipal user, IAuditTrail audit, HttpContext context, CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated == true)
        {
            await audit.RecordAsync(new AuditRecord($"user:{user.UserId()}", "SignedOut", $"user:{user.UserId()}"), cancellationToken).ConfigureAwait(false);
        }

        await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DemoSignInAsync(
        DemoSignInRequest request, IIdentityService identity, IAuditTrail audit, HttpContext context, CancellationToken cancellationToken)
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
        await audit.RecordAsync(new AuditRecord($"user:{user.UserId}", "DemoSignedIn", $"user:{user.UserId}"), cancellationToken).ConfigureAwait(false);
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

internal sealed record RegisterRequest(string DisplayName, string Email, string Password);

internal sealed record SignInRequest(string Email, string Password);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
