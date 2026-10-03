using GaiaSkyline.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace GaiaSkyline.Infrastructure.Identity;

/// <summary>
/// Identity password validator that rejects passwords found in the HIBP breach corpus. Runs on register
/// and on password change (Identity invokes all registered validators). Fails open (see the client).
/// </summary>
internal sealed class PwnedPasswordValidator(IPwnedPasswordsClient pwnedPasswords)
    : IPasswordValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user,
        string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        var pwned = await pwnedPasswords.IsPwnedAsync(password, CancellationToken.None);
        return pwned
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PwnedPassword",
                Description = "This password has appeared in a known data breach. Please choose a different one.",
            })
            : IdentityResult.Success;
    }
}
