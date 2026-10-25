using Microsoft.AspNetCore.Identity;

namespace Meridian.Infrastructure.Identity;

/// <summary>
/// Rejects passwords that appear on common-password lists, as NIST SP 800-63B recommends instead of composition rules.
/// This short embedded list keeps the demo self-contained; production would check a breached-password service
/// (e.g. the Have I Been Pwned range API, which preserves k-anonymity).
/// </summary>
internal sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public const string ErrorCode = "CommonPassword";

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1234", "password12345", "passwordpassword", "123456789012", "1234567890123", "qwertyuiop12",
        "qwertyuiopas", "iloveyou1234", "letmein12345", "welcome12345", "administrator", "changeme1234",
        "abcdefghijkl", "aaaaaaaaaaaa", "111111111111", "000000000000", "football1234", "baseball1234",
        "sunshine1234", "princess1234", "monkey123456", "dragon123456", "trustno11234", "superman1234",
        "meridian1234", "meridianmeridian",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(password is not null && Common.Contains(password)
            ? IdentityResult.Failed(new IdentityError { Code = ErrorCode, Description = "This password is too common. Choose something harder to guess." })
            : IdentityResult.Success);
}
