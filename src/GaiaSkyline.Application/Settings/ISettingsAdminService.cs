namespace GaiaSkyline.Application.Settings;

public sealed record SettingsResult(bool Ok, string? Error)
{
    public static SettingsResult Success { get; } = new(true, null);

    public static SettingsResult Fail(string error) => new(false, error);
}

public sealed record PropertyAdminDto(
    string Name,
    string RegistrationCode,
    string Address,
    double Lat,
    double Lng,
    TimeOnly CheckInFromLocal,
    TimeOnly CheckOutByLocal,
    int Sleeps,
    int Bedrooms,
    int Beds,
    int Bathrooms,
    string BedsBreakdown);

public sealed record CalendarSourceDto(
    Guid Id,
    string Name,
    string MaskedUrl,
    bool IsEnabled,
    DateTime? LastSuccessUtc,
    string? LastError,
    int ConsecutiveFailures);

/// <summary>Outcome of a non-persisting ICS "Test fetch": event count and covered range.</summary>
public sealed record TestFetchResult(bool Ok, string? Error, int Events, DateOnly? From, DateOnly? To);

/// <summary>
/// Settings writes for the admin (Stage 7 §12): the singleton property, and the external calendar
/// sources (URLs stored Data-Protection-encrypted, surfaced masked, never returned whole).
/// </summary>
public interface ISettingsAdminService
{
    Task<PropertyAdminDto?> GetPropertyAsync(CancellationToken cancellationToken);

    Task<SettingsResult> UpdatePropertyAsync(PropertyAdminDto property, CancellationToken cancellationToken);

    Task<IReadOnlyList<CalendarSourceDto>> GetCalendarSourcesAsync(CancellationToken cancellationToken);

    Task<SettingsResult> AddCalendarSourceAsync(string name, string icsUrl, bool enabled, CancellationToken cancellationToken);

    /// <summary>Replaces the stored URL (the old one is never shown; the owner pastes a fresh one).</summary>
    Task<SettingsResult> ReplaceCalendarUrlAsync(Guid id, string icsUrl, CancellationToken cancellationToken);

    Task<SettingsResult> SetCalendarEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches and parses an ICS feed WITHOUT saving anything: either a stored source (by id) or a
    /// pasted URL. Returns the event count and date range so the owner can sanity-check before enabling.
    /// </summary>
    Task<TestFetchResult> TestFetchAsync(Guid? sourceId, string? url, CancellationToken cancellationToken);
}
