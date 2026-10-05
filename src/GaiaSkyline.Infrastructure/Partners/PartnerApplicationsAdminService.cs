using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>Partner-application review (Stage 7 §11). A decision is recorded exactly once.</summary>
internal sealed class PartnerApplicationsAdminService(AppDbContext dbContext, TimeProvider clock) : IPartnerApplicationsAdminService
{
    public async Task<IReadOnlyList<PartnerApplicationDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.PartnerApplications.AsNoTracking()
            .OrderBy(a => a.Status == PartnerApplicationStatus.Pending ? 0 : 1)
            .ThenByDescending(a => a.SubmittedAtUtc)
            .Select(a => new PartnerApplicationDto(
                a.Id.Value, a.Name, a.Email, a.SocialLinks, a.AudienceSize, a.Niche, a.Message,
                a.Status.ToString(), a.SubmittedAtUtc, a.DecidedAtUtc, a.DecisionNote))
            .ToListAsync(cancellationToken);

    public Task<PartnerApplicationResult> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approve: true, note, cancellationToken);

    public Task<PartnerApplicationResult> RejectAsync(Guid id, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approve: false, note, cancellationToken);

    private async Task<PartnerApplicationResult> DecideAsync(Guid id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var application = await dbContext.PartnerApplications
            .FirstOrDefaultAsync(a => a.Id == PartnerApplicationId.From(id), cancellationToken);
        if (application is null)
        {
            return PartnerApplicationResult.Fail("That application no longer exists.");
        }

        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (approve)
            {
                application.Approve(now, note);
            }
            else
            {
                application.Reject(now, note);
            }
        }
        catch (InvalidOperationException ex)
        {
            return PartnerApplicationResult.Fail(ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return PartnerApplicationResult.Success;
    }
}
