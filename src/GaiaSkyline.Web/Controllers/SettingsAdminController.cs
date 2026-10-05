using System.Globalization;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Web.Admin;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Models;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner settings at /admin/settings (Stage 7 §12): property fields, external calendars (masked
/// URLs, test fetch, enable/disable), notification recipients with a test send, the pricing-provider
/// configuration (encrypted API key, bounds, sync interval → Hangfire rescheduled live), and the
/// per-language enable toggles (routing/hreflang/sitemap follow).
/// </summary>
[Route("admin/settings")]
public sealed class SettingsAdminController(
    ISettingsAdminService settings,
    ISiteSettingsWriter writer,
    ISiteSettings siteSettings,
    IEnabledLanguages enabledLanguages,
    IEmailSender emailSender,
    GaiaSkyline.Application.Content.IContentRevision revision,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromServices] IOptionsSnapshot<PricingProviderOptions> pricingOptions,
        [FromServices] IOptionsSnapshot<PropertyManagerOptions> pmOptions,
        [FromServices] IOptionsSnapshot<EmailOptions> emailOptions,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Settings";
        var model = new SettingsAdminViewModel(
            await settings.GetPropertyAsync(cancellationToken),
            await settings.GetCalendarSourcesAsync(cancellationToken),
            pmOptions.Value.NotificationEmails,
            emailOptions.Value.OwnerAddress,
            pricingOptions.Value,
            siteSettings.GetMasked(SettingKeys.PricingApiKey),
            SupportedCultures.All.Select(c => (c, enabledLanguages.IsEnabled(c.Slug))).ToList(),
            TestFetch: null);
        return View(model);
    }

    [HttpPost("property")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProperty(
        string name, string registrationCode, string address, string lat, string lng,
        TimeOnly? checkInFrom, TimeOnly? checkOutBy, int sleeps, int bedrooms, int beds, int bathrooms,
        string bedsBreakdown, CancellationToken cancellationToken)
    {
        if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            Toast("Coordinates must be decimal degrees (use a dot, e.g. 41.1376).", "error");
            return LocalRedirect("/admin/settings");
        }

        if (checkInFrom is not { } checkIn || checkOutBy is not { } checkOut)
        {
            Toast("Pick the check-in and check-out times.", "error");
            return LocalRedirect("/admin/settings");
        }

        var result = await settings.UpdatePropertyAsync(
            new PropertyAdminDto(name, registrationCode, address, latitude, longitude, checkIn, checkOut,
                sleeps, bedrooms, beds, bathrooms, bedsBreakdown), cancellationToken);
        return await FinishAsync(result.Ok, result.Error, "settings.property.update", "Property details saved.", cancellationToken);
    }

    [HttpPost("calendars")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCalendar(string name, string icsUrl, bool enabled, CancellationToken cancellationToken)
    {
        var result = await settings.AddCalendarSourceAsync(name, icsUrl, enabled, cancellationToken);
        return await FinishAsync(result.Ok, result.Error, "settings.calendar.add", $"Calendar {name} added.", cancellationToken);
    }

    [HttpPost("calendars/{id:guid}/url")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceCalendarUrl(Guid id, string icsUrl, CancellationToken cancellationToken)
    {
        var result = await settings.ReplaceCalendarUrlAsync(id, icsUrl, cancellationToken);
        return await FinishAsync(result.Ok, result.Error, "settings.calendar.replace-url", "Calendar URL replaced.", cancellationToken);
    }

    [HttpPost("calendars/{id:guid}/enabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCalendarEnabled(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var result = await settings.SetCalendarEnabledAsync(id, enabled, cancellationToken);
        return await FinishAsync(result.Ok, result.Error, enabled ? "settings.calendar.enable" : "settings.calendar.disable",
            enabled ? "Calendar enabled — imports resume on the next sync." : "Calendar disabled.", cancellationToken);
    }

    [HttpPost("calendars/test-fetch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestFetch(
        Guid? sourceId, string? url,
        [FromServices] IOptionsSnapshot<PricingProviderOptions> pricingOptions,
        [FromServices] IOptionsSnapshot<PropertyManagerOptions> pmOptions,
        [FromServices] IOptionsSnapshot<EmailOptions> emailOptions,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Settings";
        var fetch = await settings.TestFetchAsync(sourceId, url, cancellationToken);
        var model = new SettingsAdminViewModel(
            await settings.GetPropertyAsync(cancellationToken),
            await settings.GetCalendarSourcesAsync(cancellationToken),
            pmOptions.Value.NotificationEmails,
            emailOptions.Value.OwnerAddress,
            pricingOptions.Value,
            siteSettings.GetMasked(SettingKeys.PricingApiKey),
            SupportedCultures.All.Select(c => (c, enabledLanguages.IsEnabled(c.Slug))).ToList(),
            fetch);
        return View("Index", model);
    }

    [HttpPost("notifications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotifications(string? pmEmails, string? ownerEmail, CancellationToken cancellationToken)
    {
        await writer.SetAsync(SettingKeys.PropertyManagerEmails, NormalizeEmails(pmEmails), isSecret: false, cancellationToken);
        await writer.SetAsync(SettingKeys.OwnerEmail, ownerEmail, isSecret: false, cancellationToken);
        return await FinishAsync(true, null, "settings.notifications.update", "Notification recipients saved.", cancellationToken);
    }

    [HttpPost("notifications/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(
        [FromServices] IOptionsSnapshot<PropertyManagerOptions> pmOptions,
        [FromServices] IOptionsSnapshot<EmailOptions> emailOptions,
        CancellationToken cancellationToken)
    {
        var recipients = pmOptions.Value.NotificationEmails
            .Append(emailOptions.Value.OwnerAddress)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var recipient in recipients)
        {
            await emailSender.SendAsync(new EmailMessage(
                recipient, recipient, "Gaia Skyline — test notification",
                "<p>This is a test notification from /admin/settings. If you can read this, delivery works.</p>"),
                cancellationToken);
        }

        await audit.WriteAsync("settings.notifications.test", ActorId, Ip, "Settings", null,
            new { recipients = recipients.Count }, cancellationToken);
        Toast($"Test email sent to {recipients.Count} recipient(s).");
        return LocalRedirect("/admin/settings");
    }

    [HttpPost("pricing")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePricing(
        string provider, string? apiKey, string? listingId, string? adjustmentPct, string? floor, string? ceiling,
        int syncIntervalHours, [FromServices] IRecurringJobManager recurringJobs, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RateProviderType>(provider, ignoreCase: true, out var parsedProvider))
        {
            Toast("Pick a valid provider.", "error");
            return LocalRedirect("/admin/settings");
        }

        if (!AdminMoney.TryParse(adjustmentPct, out var adjustment)
            || !AdminMoney.TryParse(floor, out var floorEur)
            || !AdminMoney.TryParse(ceiling, out var ceilingEur))
        {
            Toast("Adjustment, floor and ceiling must be numbers.", "error");
            return LocalRedirect("/admin/settings");
        }

        if (syncIntervalHours is < 1 or > 24)
        {
            Toast("The sync interval must be between 1 and 24 hours.", "error");
            return LocalRedirect("/admin/settings");
        }

        await writer.SetAsync(SettingKeys.PricingProvider, parsedProvider.ToString(), isSecret: false, cancellationToken);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            // Blank keeps the stored key; the field never shows the stored value.
            await writer.SetAsync(SettingKeys.PricingApiKey, apiKey, isSecret: true, cancellationToken);
        }

        await writer.SetAsync(SettingKeys.PricingListingId, listingId, isSecret: false, cancellationToken);
        await writer.SetAsync(SettingKeys.PricingAdjustmentPct, Invariant(adjustment), isSecret: false, cancellationToken);
        await writer.SetAsync(SettingKeys.PricingFloor, Invariant(floorEur), isSecret: false, cancellationToken);
        await writer.SetAsync(SettingKeys.PricingCeiling, Invariant(ceilingEur), isSecret: false, cancellationToken);
        await writer.SetAsync(SettingKeys.PricingSyncIntervalHours,
            syncIntervalHours.ToString(CultureInfo.InvariantCulture), isSecret: false, cancellationToken);

        // Re-schedule the sync job to the new cadence immediately (same id/method as Program.cs).
        recurringJobs.AddOrUpdate<IRateSyncService>(
            "rate-sync", service => service.SyncAsync(CancellationToken.None), $"0 */{syncIntervalHours} * * *");

        return await FinishAsync(true, null, "settings.pricing.update",
            parsedProvider == RateProviderType.None
                ? "Pricing set to manual mode."
                : $"Pricing provider saved ({parsedProvider}). Adapter not yet available — the sync stays dormant until it ships.",
            cancellationToken);
    }

    [HttpPost("languages")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLanguages(string[] enabled, CancellationToken cancellationToken)
    {
        var slugs = SupportedCultures.All.Select(c => c.Slug).ToList();
        var chosen = enabled.Intersect(slugs, StringComparer.OrdinalIgnoreCase)
            .Append(SupportedCultures.DefaultSlug)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // All languages on = no override row.
        var value = chosen.Count == slugs.Count ? null : string.Join(',', chosen);
        await writer.SetAsync(SettingKeys.EnabledLanguages, value, isSecret: false, cancellationToken);

        // Cached public pages carry the hreflang set (and the sitemap its URL list) — refresh them now.
        revision.Bump();
        return await FinishAsync(true, null, "settings.languages.update",
            "Languages saved — disabled ones left routing, hreflang and the sitemap.", cancellationToken);
    }

    private async Task<IActionResult> FinishAsync(
        bool ok, string? error, string auditEvent, string success, CancellationToken cancellationToken)
    {
        if (!ok)
        {
            Toast(error ?? "The change could not be saved.", "error");
            return LocalRedirect("/admin/settings");
        }

        await audit.WriteAsync(auditEvent, ActorId, Ip, "Settings", null, null, cancellationToken);
        Toast(success);
        return LocalRedirect("/admin/settings");
    }

    private static string? NormalizeEmails(string? emails) =>
        string.IsNullOrWhiteSpace(emails)
            ? null
            : string.Join(';', emails
                .Split([';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@', StringComparison.Ordinal)));

    private static string Invariant(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}
