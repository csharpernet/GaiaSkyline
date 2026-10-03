using Microsoft.AspNetCore.Identity;

namespace GaiaSkyline.Infrastructure.Identity;

/// <summary>
/// The application user. Guid-keyed to match the domain's strongly-typed id vocabulary.
/// <see cref="PreferredLanguage"/> drives the language of auth emails and the guest area.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string PreferredLanguage { get; set; } = "en";

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Guid-keyed role, so roles share the key type with users.</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
