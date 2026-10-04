using System.Globalization;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Admin;

/// <summary>
/// Emails the owner a daily digest of manual-sync items that are overdue (change not mirrored in Hostify
/// within 24 h). Sends nothing when there is nothing overdue, so the 09:00 job is a no-op on quiet days.
/// </summary>
internal sealed class ManualSyncReminderService(
    IDashboardService dashboard,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    ILogger<ManualSyncReminderService> logger) : IManualSyncReminderService
{
    public async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var items = await dashboard.GetOutstandingManualSyncAsync(cancellationToken);
        var overdue = items.Where(i => i.Overdue).ToList();
        if (overdue.Count == 0)
        {
            logger.LogInformation("Manual-sync reminder: nothing overdue.");
            return;
        }

        var rows = string.Join(string.Empty, items.Select(i =>
        {
            var verb = i.Action == ManualSyncAction.BlockInHostify ? "Block" : "Unblock";
            var dates = $"{i.CheckIn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} – {i.CheckOut.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";
            var flag = i.Overdue ? " <strong>(overdue)</strong>" : string.Empty;
            return $"<li>{verb} in Hostify: {dates} — booking {i.Reference} ({i.GuestName}){flag}</li>";
        }));

        var body =
            $"<p><strong>{overdue.Count} direct booking change(s) still need mirroring in Hostify.</strong></p>" +
            $"<ul>{rows}</ul>" +
            "<p>Open the admin dashboard and tick \"Done in Hostify\" once each is handled.</p>";

        await emailSender.SendAsync(
            new EmailMessage(emailOptions.Value.OwnerAddress, emailOptions.Value.FromName,
                $"Action needed: {overdue.Count} booking(s) to mirror in Hostify", body),
            cancellationToken);
    }
}
