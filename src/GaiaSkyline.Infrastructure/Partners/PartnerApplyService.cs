using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// Saves a public partner application (Stage 8 Part A) and emails the owner. A pending application for the
/// same email is updated rather than duplicated, so a double-submit stays tidy.
/// </summary>
internal sealed class PartnerApplyService(
    AppDbContext dbContext,
    IEmailSender emailSender,
    ISiteSettings settings,
    IOptions<EmailOptions> emailOptions,
    TimeProvider clock) : IPartnerApplyService
{
    public async Task<PartnerApplyResult> SubmitAsync(
        PartnerApplySubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (string.IsNullOrWhiteSpace(submission.Name) || string.IsNullOrWhiteSpace(submission.Email))
        {
            return PartnerApplyResult.Fail("Name and email are required.");
        }

        if (!submission.Email.Contains('@', StringComparison.Ordinal))
        {
            return PartnerApplyResult.Fail("That email address does not look right.");
        }

        var email = submission.Email.Trim();
        var pending = await dbContext.PartnerApplications
            .FirstOrDefaultAsync(
                a => a.Email == email && a.Status == PartnerApplicationStatus.Pending, cancellationToken);
        if (pending is not null)
        {
            // Re-applying while pending just refreshes the pending row — no duplicates in the review queue.
            dbContext.PartnerApplications.Remove(pending);
        }

        var application = new PartnerApplication(
            PartnerApplicationId.New(),
            submission.Name,
            email,
            submission.SocialLinks,
            submission.AudienceSize,
            submission.Niche,
            submission.Message,
            clock.GetUtcNow().UtcDateTime);
        dbContext.PartnerApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        var ownerEmail = settings.Get(SettingKeys.OwnerEmail) ?? emailOptions.Value.OwnerAddress;
        await emailSender.SendAsync(
            new EmailMessage(
                ownerEmail,
                "Gaia Skyline owner",
                $"New partner application — {application.Name}",
                $"<p><strong>{application.Name}</strong> ({application.Email}) applied to the partner program.</p>"
                + $"<p>Audience: {application.AudienceSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—"}"
                + $" · Niche: {application.Niche ?? "—"}<br/>Socials: {application.SocialLinks ?? "—"}</p>"
                + $"<p>{application.Message ?? string.Empty}</p>"
                + "<p>Review it under <a href=\"/admin/partners\">Admin → Partners</a>.</p>"),
            cancellationToken);

        return PartnerApplyResult.Success();
    }
}
