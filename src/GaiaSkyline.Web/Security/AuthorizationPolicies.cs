namespace GaiaSkyline.Web.Security;

/// <summary>Named authorization policies used across the admin, partner and guest surfaces.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Owner role + IP allowlist (and, in practice, a 2FA-completed cookie).</summary>
    public const string Owner = "OwnerOnly";

    public const string Partner = "PartnerOnly";

    public const string Guest = "GuestOnly";
}
