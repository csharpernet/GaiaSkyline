namespace GaiaSkyline.Application.Notifications;

/// <summary>Email configuration, bound from the <c>Email</c> section.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Smtp" (dev, smtp4dev) or "SendGrid" (prod).</summary>
    public string Provider { get; set; } = "Smtp";

    public string FromAddress { get; set; } = "stay@gaiaskyline.com";

    public string FromName { get; set; } = "Gaia Skyline";

    /// <summary>Where owner notifications (new bookings, disputes) are sent — in English.</summary>
    public string OwnerAddress { get; set; } = "nuno_senica@hotmail.com";

    // SMTP (dev).
    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 2525;

    // SendGrid (prod). The key lives in User Secrets / environment, never in source.
    public string SendGridApiKey { get; set; } = string.Empty;

    /// <summary>Base URL used to build absolute links (confirmation, invoice) in emails.</summary>
    public string SiteBaseUrl { get; set; } = "https://localhost:7443";
}
