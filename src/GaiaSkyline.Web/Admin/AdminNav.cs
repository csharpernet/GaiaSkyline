namespace GaiaSkyline.Web.Admin;

/// <summary>One entry in the admin sidebar. <see cref="Enabled"/> flips to true as each Stage 7
/// increment lands its section; disabled items render greyed with a "soon" tag (no dead links).</summary>
public sealed record AdminNavItem(string Label, string Href, bool Enabled, string? Shortcut = null);

/// <summary>
/// The admin information architecture, rendered by <c>_AdminShell</c>. Keeping it in one place means the
/// sidebar, keyboard shortcuts and the "coming soon" affordance all stay in sync as sections ship.
/// </summary>
public static class AdminNav
{
    public static IReadOnlyList<AdminNavItem> Items { get; } =
    [
        new("Dashboard", "/admin", Enabled: true, Shortcut: "g h"),
        new("Content", "/admin/content", Enabled: true),
        new("Media", "/admin/media", Enabled: true),
        new("Stories", "/admin/stories", Enabled: true),
        new("Bookings", "/admin/bookings", Enabled: false, Shortcut: "g b"),
        new("Calendar", "/admin/calendar", Enabled: false, Shortcut: "g c"),
        new("Prices", "/admin/prices", Enabled: false),
        new("Payments", "/admin/payments", Enabled: false),
        new("Reviews", "/admin/reviews", Enabled: false),
        new("Partners", "/admin/partners", Enabled: false),
        new("SEO", "/admin/seo", Enabled: false),
        new("Settings", "/admin/settings", Enabled: false),
        new("Audit log", "/admin/audit", Enabled: true),
    ];
}
