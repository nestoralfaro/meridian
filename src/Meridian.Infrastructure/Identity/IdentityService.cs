using Meridian.Application.Identity;
using Meridian.Infrastructure.DemoData;
using Microsoft.AspNetCore.Identity;

namespace Meridian.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<ApplicationUser> users) : IIdentityService
{
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
}
