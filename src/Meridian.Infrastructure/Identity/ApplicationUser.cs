using Microsoft.AspNetCore.Identity;

namespace Meridian.Infrastructure.Identity;

/// <summary>A sign-in account, linked to the client (person) it belongs to.</summary>
internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid ClientId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Demo personas sign in without a password and can't change credentials.</summary>
    public bool IsDemo { get; set; }
}
