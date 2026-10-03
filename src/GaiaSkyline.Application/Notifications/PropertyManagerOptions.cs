namespace GaiaSkyline.Application.Notifications;

/// <summary>
/// Where to copy direct-booking alerts for the management company. Empty by default (admin-editable in
/// Stage 7); when empty, the property-manager email is skipped.
/// </summary>
public sealed class PropertyManagerOptions
{
    public const string SectionName = "PropertyManager";

    public IReadOnlyList<string> NotificationEmails { get; set; } = [];
}
