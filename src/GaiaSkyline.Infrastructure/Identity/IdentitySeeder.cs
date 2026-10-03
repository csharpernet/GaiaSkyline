using GaiaSkyline.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace GaiaSkyline.Infrastructure.Identity;

/// <summary>
/// Ensures the three roles exist and provisions the Owner. The Owner is seeded from configuration in
/// Development and created in production via the one-off <c>create-owner</c> CLI command. There is no
/// self-registration path to the Owner role.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    TimeProvider clock)
{
    public async Task EnsureRolesAsync()
    {
        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {Describe(result)}");
                }
            }
        }
    }

    /// <summary>Creates the Owner if absent (idempotent). Returns true when a new Owner was created.</summary>
    public async Task<bool> EnsureOwnerAsync(string email, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await EnsureRolesAsync();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, UserRoles.Owner))
            {
                await userManager.AddToRoleAsync(existing, UserRoles.Owner);
            }

            return false;
        }

        var owner = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PreferredLanguage = "en",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        var created = await userManager.CreateAsync(owner, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create Owner: {Describe(created)}");
        }

        await userManager.AddToRoleAsync(owner, UserRoles.Owner);
        return true;
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
