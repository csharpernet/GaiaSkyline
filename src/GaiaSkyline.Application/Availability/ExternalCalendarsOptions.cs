namespace GaiaSkyline.Application.Availability;

/// <summary>
/// External calendar sources from configuration (<c>ExternalCalendars:Sources</c> in User Secrets /
/// Key Vault), so a URL can be added without code changes. Empty by default — the admin screen in
/// Stage 7 manages these too. The URL is a credential; it is encrypted before it is stored.
/// </summary>
public sealed class ExternalCalendarsOptions
{
    public const string SectionName = "ExternalCalendars";

    public IReadOnlyList<ExternalCalendarSourceConfig> Sources { get; set; } = [];
}

public sealed class ExternalCalendarSourceConfig
{
    public string Name { get; set; } = string.Empty;

    public string IcsUrl { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
