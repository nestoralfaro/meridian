using System.Security.Claims;
using Meridian.Application.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Meridian.Api.Auth;

/// <summary>
/// Cookie session (ADR-0004: same origin, so no tokens in browser storage) plus antiforgery protection that matches
/// Angular's built-in XSRF support (it reads <c>XSRF-TOKEN</c> and sends <c>X-XSRF-TOKEN</c> on state-changing requests).
/// </summary>
internal static class AuthSetup
{
    public const string SessionCookie = "meridian.session";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

    private static bool SecureCookies { get; set; } = true;

    /// <param name="secureCookies">
    /// Mark cookies Secure (HTTPS only). Always true in Azure (TLS terminates at the ingress; forwarded headers make
    /// requests HTTPS). Only the local docker compose stack, which serves plain HTTP, turns it off (Auth:SecureCookies).
    /// </param>
    public static IServiceCollection AddMeridianAuth(this IServiceCollection services, bool secureCookies = true)
    {
        var securePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        SecureCookies = secureCookies;

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookie;
                options.Cookie.HttpOnly = true;                       // not readable by scripts
                options.Cookie.SecurePolicy = securePolicy;
                options.Cookie.SameSite = SameSiteMode.Strict;       // never sent on cross-site requests
                options.ExpireTimeSpan = IdleTimeout;                 // idle timeout, renewed by activity
                options.SlidingExpiration = true;
                // An API answers 401/403; it never redirects to a login page.
                options.Events.OnRedirectToLogin = context => Respond(context.Response, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => Respond(context.Response, StatusCodes.Status403Forbidden);
            });
        services.AddAuthorization();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfHeader;
            options.Cookie.Name = "meridian.af";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
        });

        return services;
    }

    public static ClaimsPrincipal ToPrincipal(AuthenticatedUser user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(MeridianClaims.ClientId, user.ClientId.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(MeridianClaims.Demo, user.IsDemo ? "true" : "false"),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));

    public static Task SignInAsync(HttpContext context, AuthenticatedUser user) =>
        context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ToPrincipal(user));

    /// <summary>Issues a fresh antiforgery token pair: an HttpOnly cookie plus a script-readable request token.</summary>
    public static void IssueAntiforgeryToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // Angular must read it to echo it in the X-XSRF-TOKEN header
            Secure = SecureCookies || context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }

    private static Task Respond(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}

internal static class MeridianClaims
{
    public const string ClientId = "meridian:client_id";
    public const string Demo = "meridian:demo";

    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid ClientIdOf(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClientId)!);

    public static bool IsDemo(this ClaimsPrincipal user) => user.FindFirstValue(Demo) == "true";
}

/// <summary>Rejects state-changing requests that don't carry a valid antiforgery token (CSRF protection).</summary>
internal sealed class ValidateAntiforgery(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing or invalid antiforgery token.");
        }

        return await next(context).ConfigureAwait(false);
    }
}
