using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Web.Localization;

namespace GaiaSkyline.Web.Models;

/// <summary>/admin/settings: property, calendars, notifications, pricing provider and languages.</summary>
public sealed record SettingsAdminViewModel(
    PropertyAdminDto? Property,
    IReadOnlyList<CalendarSourceDto> Calendars,
    IReadOnlyList<string> PropertyManagerEmails,
    string OwnerEmail,
    PricingProviderOptions Pricing,
    string? PricingApiKeyMasked,
    IReadOnlyList<(CultureOption Culture, bool Enabled)> Languages,
    TestFetchResult? TestFetch);
