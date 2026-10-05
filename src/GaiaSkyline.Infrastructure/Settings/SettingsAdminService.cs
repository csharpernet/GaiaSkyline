using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Settings;

/// <summary>
/// Property + external-calendar settings for the admin (Stage 7 §12). Calendar URLs are stored
/// encrypted (ExternalCalendarUrlProtector) and only ever surfaced masked; "Test fetch" downloads and
/// parses a feed without persisting anything. Property edits bump the content revision so the public
/// pages (JSON-LD, check-in times) refresh.
/// </summary>
internal sealed class SettingsAdminService(
    AppDbContext dbContext,
    ExternalCalendarUrlProtector urlProtector,
    IHttpClientFactory httpClientFactory,
    IContentRevision revision,
    TimeProvider clock) : ISettingsAdminService
{
    public async Task<PropertyAdminDto?> GetPropertyAsync(CancellationToken cancellationToken)
    {
        var property = await dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return property is null
            ? null
            : new PropertyAdminDto(
                property.Name, property.RegistrationCode, property.Address, property.Lat, property.Lng,
                property.CheckInFromLocal, property.CheckOutByLocal, property.Sleeps, property.Bedrooms,
                property.Beds, property.Bathrooms, property.BedsBreakdown);
    }

    public async Task<SettingsResult> UpdatePropertyAsync(PropertyAdminDto model, CancellationToken cancellationToken)
    {
        var property = await dbContext.Properties.FirstOrDefaultAsync(cancellationToken);
        if (property is null)
        {
            return SettingsResult.Fail("The property row is missing — run the content seeder first.");
        }

        try
        {
            property.Update(
                model.Name, model.RegistrationCode, model.Address, model.Lat, model.Lng,
                model.CheckInFromLocal, model.CheckOutByLocal, model.Sleeps, model.Bedrooms,
                model.Beds, model.Bathrooms, model.BedsBreakdown);
        }
        catch (ArgumentException ex)
        {
            return SettingsResult.Fail(ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return SettingsResult.Success;
    }

    public async Task<IReadOnlyList<CalendarSourceDto>> GetCalendarSourcesAsync(CancellationToken cancellationToken)
    {
        var sources = await dbContext.ExternalCalendarSources.AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return sources.Select(s => new CalendarSourceDto(
            s.Id.Value, s.Name, MaskedUrl(s.IcsUrlProtected), s.IsEnabled,
            s.LastSuccessUtc, s.LastError, s.ConsecutiveFailures)).ToList();
    }

    public async Task<SettingsResult> AddCalendarSourceAsync(string name, string icsUrl, bool enabled, CancellationToken cancellationToken)
    {
        if (!IsHttpUrl(icsUrl))
        {
            return SettingsResult.Fail("Paste a full http(s) ICS URL.");
        }

        if (await dbContext.ExternalCalendarSources.AnyAsync(s => s.Name == name.Trim(), cancellationToken))
        {
            return SettingsResult.Fail($"A calendar named {name.Trim()} already exists.");
        }

        try
        {
            dbContext.ExternalCalendarSources.Add(new ExternalCalendarSource(
                ExternalCalendarSourceId.New(), name, urlProtector.Protect(icsUrl.Trim()),
                enabled, clock.GetUtcNow().UtcDateTime));
        }
        catch (ArgumentException ex)
        {
            return SettingsResult.Fail(ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return SettingsResult.Success;
    }

    public async Task<SettingsResult> ReplaceCalendarUrlAsync(Guid id, string icsUrl, CancellationToken cancellationToken)
    {
        if (!IsHttpUrl(icsUrl))
        {
            return SettingsResult.Fail("Paste a full http(s) ICS URL.");
        }

        var source = await FindAsync(id, cancellationToken);
        if (source is null)
        {
            return SettingsResult.Fail("That calendar no longer exists.");
        }

        source.UpdateUrl(urlProtector.Protect(icsUrl.Trim()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return SettingsResult.Success;
    }

    public async Task<SettingsResult> SetCalendarEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var source = await FindAsync(id, cancellationToken);
        if (source is null)
        {
            return SettingsResult.Fail("That calendar no longer exists.");
        }

        source.SetEnabled(enabled);
        await dbContext.SaveChangesAsync(cancellationToken);
        return SettingsResult.Success;
    }

    public async Task<TestFetchResult> TestFetchAsync(Guid? sourceId, string? url, CancellationToken cancellationToken)
    {
        string target;
        if (sourceId is { } id)
        {
            var source = await FindAsync(id, cancellationToken);
            if (source is null)
            {
                return new TestFetchResult(false, "That calendar no longer exists.", 0, null, null);
            }

            target = urlProtector.Unprotect(source.IcsUrlProtected);
        }
        else if (IsHttpUrl(url))
        {
            target = url!.Trim();
        }
        else
        {
            return new TestFetchResult(false, "Paste a full http(s) ICS URL.", 0, null, null);
        }

        try
        {
            using var client = httpClientFactory.CreateClient("ics-test-fetch");
            client.Timeout = TimeSpan.FromSeconds(15);
            var ics = await client.GetStringAsync(new Uri(target), cancellationToken);
            var events = IcsParser.Parse(ics);
            return new TestFetchResult(
                true, null, events.Count,
                events.Count > 0 ? events.Min(e => e.Start) : null,
                events.Count > 0 ? events.Max(e => e.EndInclusive) : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or FormatException)
        {
            return new TestFetchResult(false, $"Fetch failed: {ex.Message}", 0, null, null);
        }
    }

    private Task<ExternalCalendarSource?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ExternalCalendarSources
            .FirstOrDefaultAsync(s => s.Id == ExternalCalendarSourceId.From(id), cancellationToken);

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    /// <summary>Masking works on the PLAINTEXT url (last 4 chars); the stored value is ciphertext.</summary>
    private string MaskedUrl(string protectedUrl)
    {
        try
        {
            return ExternalCalendarUrlProtector.Mask(urlProtector.Unprotect(protectedUrl));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return "••••";
        }
    }
}
