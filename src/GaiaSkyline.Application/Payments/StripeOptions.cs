namespace GaiaSkyline.Application.Payments;

/// <summary>
/// Stripe configuration, bound from the <c>Stripe</c> section. Keys live in User Secrets (dev) or the
/// environment (prod) — never in source or appsettings.
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string PublishableKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Multibanco needs at least this many days before check-in; excluded otherwise.</summary>
    public int MultibancoMinLeadDays { get; set; } = 10;

    /// <summary>Minutes a card/wallet booking may sit unpaid before its hold is released.</summary>
    public int UnpaidHoldMinutes { get; set; } = 30;
}
