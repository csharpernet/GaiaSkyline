namespace GaiaSkyline.Web.Security;

/// <summary>JWT settings for the partner API, bound from the <c>Jwt</c> section (key from User Secrets).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "GaiaSkyline";

    public string Audience { get; set; } = "GaiaSkylinePartners";

    public int AccessTokenMinutes { get; set; } = 15;
}

/// <summary>Authentication scheme names used by attribute-level authorization.</summary>
public static class AuthSchemes
{
    public const string PartnerJwt = "PartnerJwt";
}
