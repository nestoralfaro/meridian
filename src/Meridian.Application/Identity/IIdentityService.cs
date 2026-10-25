namespace Meridian.Application.Identity;

/// <summary>A user whose identity has been verified; the host turns this into a session (claims + cookie).</summary>
public sealed record AuthenticatedUser(Guid UserId, Guid ClientId, string DisplayName, string Email, bool IsDemo);

/// <summary>The ready-made demo clients a visitor can sign in as without credentials.</summary>
public enum DemoPersona
{
    Dana,
    Raj,
}

public enum ChangePasswordOutcome
{
    Changed,
    DemoAccount,
    IncorrectCurrentPassword,
    NewPasswordRejected,
    UserNotFound,
}

public sealed record ChangePasswordResult(ChangePasswordOutcome Outcome, IReadOnlyList<string> Errors)
{
    public static ChangePasswordResult Of(ChangePasswordOutcome outcome) => new(outcome, []);
}

public sealed record RegistrationResult(AuthenticatedUser? User, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool Succeeded => User is not null;
}

public enum SignInOutcome
{
    Succeeded,
    Failed,
    LockedOut,
}

public sealed record SignInAttempt(SignInOutcome Outcome, AuthenticatedUser? User);

/// <summary>User accounts and credential checks. Session handling (cookies) belongs to the host.</summary>
public interface IIdentityService
{
    Task<AuthenticatedUser?> FindDemoPersonaAsync(DemoPersona persona, CancellationToken cancellationToken = default);

    /// <summary>Creates a client and their sign-in account. Errors are keyed by field (displayName, email, password).</summary>
    Task<RegistrationResult> RegisterAsync(string displayName, string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Checks credentials with lockout. Demo personas never sign in with a password.</summary>
    Task<SignInAttempt> VerifyPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
