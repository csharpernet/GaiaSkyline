namespace GaiaSkyline.Domain.Identity;

/// <summary>
/// The three application roles. Owner administers the site (requires 2FA + IP allowlist); Partner is
/// a co-host/supplier (onboarding arrives in Stage 8); Guest is an optional booking account.
/// </summary>
public static class UserRoles
{
    public const string Owner = "Owner";
    public const string Partner = "Partner";
    public const string Guest = "Guest";

    public static readonly IReadOnlyList<string> All = [Owner, Partner, Guest];
}
