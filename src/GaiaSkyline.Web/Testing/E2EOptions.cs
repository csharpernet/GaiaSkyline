namespace GaiaSkyline.Web.Testing;

/// <summary>
/// Configuration for the end-to-end test seam (bound from the <c>E2E</c> section). It is OFF by default
/// and must never be enabled in Production — <c>Program.cs</c> throws at startup if it is. When enabled,
/// outbound email is captured in memory (so the Playwright suite can read a magic link), and a
/// deterministic Owner authenticator key plus a known booking are seeded.
/// </summary>
public sealed class E2EOptions
{
    public const string SectionName = "E2E";

    /// <summary>Master switch. Default false; only ever true in the CI browser-quality job or local E2E.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base32 authenticator key the seeded Owner is given, so a test can compute a valid TOTP. This is a
    /// non-secret test fixture — useless without the (ephemeral, per-run) Owner password.
    /// </summary>
    public string OwnerTotpKey { get; set; } = "JBSWY3DPEHPK3PXP";

    /// <summary>Reference of the booking seeded for the guest magic-link test.</summary>
    public string BookingReference { get; set; } = "GS-E2E01";

    /// <summary>Guest email on the seeded booking.</summary>
    public string GuestEmail { get; set; } = "guest.e2e@example.com";
}
