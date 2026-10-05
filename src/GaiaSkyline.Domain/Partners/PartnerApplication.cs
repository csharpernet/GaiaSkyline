using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Partners;

public enum PartnerApplicationStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// An influencer/partner-program application (Stage 7 §11 reviews them; the program itself ships in
/// Stage 8). Decisions are recorded once; a decided application never flips back to pending.
/// </summary>
public sealed class PartnerApplication : Entity<PartnerApplicationId>
{
    // Required by EF Core's materialization.
    private PartnerApplication()
    {
    }

    public PartnerApplication(
        PartnerApplicationId id,
        string name,
        string email,
        string? socialLinks,
        int? audienceSize,
        string? niche,
        string? message,
        DateTime submittedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (audienceSize is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(audienceSize), "Audience size cannot be negative.");
        }

        Id = id;
        Name = name.Trim();
        Email = email.Trim();
        SocialLinks = string.IsNullOrWhiteSpace(socialLinks) ? null : socialLinks.Trim();
        AudienceSize = audienceSize;
        Niche = string.IsNullOrWhiteSpace(niche) ? null : niche.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Status = PartnerApplicationStatus.Pending;
        SubmittedAtUtc = submittedAtUtc;
    }

    public string Name { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    /// <summary>Free-text social profiles (one or more URLs/handles).</summary>
    public string? SocialLinks { get; private set; }

    public int? AudienceSize { get; private set; }

    public string? Niche { get; private set; }

    public string? Message { get; private set; }

    public PartnerApplicationStatus Status { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    /// <summary>The owner's note recorded with the decision (e.g. why it was rejected).</summary>
    public string? DecisionNote { get; private set; }

    public void Approve(DateTime atUtc, string? note) => Decide(PartnerApplicationStatus.Approved, atUtc, note);

    public void Reject(DateTime atUtc, string? note) => Decide(PartnerApplicationStatus.Rejected, atUtc, note);

    private void Decide(PartnerApplicationStatus status, DateTime atUtc, string? note)
    {
        if (Status != PartnerApplicationStatus.Pending)
        {
            throw new InvalidOperationException("This application was already decided.");
        }

        Status = status;
        DecidedAtUtc = atUtc;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
