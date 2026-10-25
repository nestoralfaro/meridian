using Meridian.Application.Identity;
using Meridian.Domain;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Meridian.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<ApplicationUser> users, MeridianDbContext db) : IIdentityService
{
    /// <summary>
    /// Verified against when the email is unknown, so the response takes about as long as a real password check and
    /// timing doesn't reveal whether an account exists.
    /// </summary>
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    public async Task<AuthenticatedUser?> FindDemoPersonaAsync(DemoPersona persona, CancellationToken cancellationToken = default)
    {
        var email = persona switch
        {
            DemoPersona.Dana => DemoPortfolio.DanaEmail,
            DemoPersona.Raj => DemoPortfolio.RajEmail,
            _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, null),
        };
        var user = await users.FindByEmailAsync(email).ConfigureAwait(false);
        return user is { IsDemo: true } ? ToAuthenticated(user) : null;
    }

    public async Task<RegistrationResult> RegisterAsync(string displayName, string email, string password, CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        email = email.Trim();
        if (displayName.Length is 0 or > 100)
        {
            return Rejected("displayName", "Enter your name (up to 100 characters).");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var client = new Client(Guid.CreateVersion7(), displayName, email);
        db.Add(client);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var user = new ApplicationUser { Id = client.Id, ClientId = client.Id, DisplayName = displayName, UserName = email, Email = email };
        var result = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new RegistrationResult(null, result.Errors
                .GroupBy(e => FieldFor(e.Code))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new RegistrationResult(ToAuthenticated(user), new Dictionary<string, string[]>());
    }

    public async Task<SignInAttempt> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(email.Trim()).ConfigureAwait(false);
        if (user is null || user.IsDemo || user.PasswordHash is null)
        {
            new PasswordHasher<ApplicationUser>().VerifyHashedPassword(new ApplicationUser(), DummyHash.Value, password);
            return new SignInAttempt(SignInOutcome.Failed, null);
        }

        if (await users.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return new SignInAttempt(SignInOutcome.LockedOut, null);
        }

        if (await users.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
            return new SignInAttempt(SignInOutcome.Succeeded, ToAuthenticated(user));
        }

        await users.AccessFailedAsync(user).ConfigureAwait(false);
        return await users.IsLockedOutAsync(user).ConfigureAwait(false)
            ? new SignInAttempt(SignInOutcome.LockedOut, null)
            : new SignInAttempt(SignInOutcome.Failed, null);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return ChangePasswordResult.Of(ChangePasswordOutcome.UserNotFound);
        }

        if (user.IsDemo)
        {
            return ChangePasswordResult.Of(ChangePasswordOutcome.DemoAccount);
        }

        if (!await users.CheckPasswordAsync(user, currentPassword).ConfigureAwait(false))
        {
            return ChangePasswordResult.Of(ChangePasswordOutcome.IncorrectCurrentPassword);
        }

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword).ConfigureAwait(false);
        return result.Succeeded
            ? ChangePasswordResult.Of(ChangePasswordOutcome.Changed)
            : new ChangePasswordResult(ChangePasswordOutcome.NewPasswordRejected, [.. result.Errors.Select(e => e.Description)]);
    }

    internal static AuthenticatedUser ToAuthenticated(ApplicationUser user) =>
        new(user.Id, user.ClientId, user.DisplayName, user.Email!, user.IsDemo);

    private static RegistrationResult Rejected(string field, string message) =>
        new(null, new Dictionary<string, string[]> { [field] = [message] });

    private static string FieldFor(string identityErrorCode) => identityErrorCode switch
    {
        _ when identityErrorCode.StartsWith("Password", StringComparison.Ordinal) => "password",
        CommonPasswordValidator.ErrorCode => "password",
        _ when identityErrorCode.Contains("Email", StringComparison.Ordinal) || identityErrorCode.Contains("UserName", StringComparison.Ordinal) => "email",
        _ => "form",
    };
}
