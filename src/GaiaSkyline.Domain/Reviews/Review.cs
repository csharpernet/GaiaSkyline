using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Reviews;

/// <summary>A guest review shown on the public site. Only published reviews are returned.</summary>
public sealed class Review : Entity<ReviewId>
{
    // Required by EF Core's materialization.
    private Review()
    {
    }

    public Review(
        ReviewId id,
        int rating,
        string guestFirstName,
        string? guestLocation,
        string body,
        string source,
        DateOnly stayedOn,
        bool isPublished)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, 5);

        Id = id;
        Rating = rating;
        GuestFirstName = guestFirstName.Trim();
        GuestLocation = string.IsNullOrWhiteSpace(guestLocation) ? null : guestLocation.Trim();
        Body = body.Trim();
        Source = source.Trim();
        StayedOn = stayedOn;
        IsPublished = isPublished;
    }

    /// <summary>Admin edit (typo fixes, rating corrections) with the same validation as creation.</summary>
    public void Update(int rating, string guestFirstName, string? guestLocation, string body, string source, DateOnly stayedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestFirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, 5);

        Rating = rating;
        GuestFirstName = guestFirstName.Trim();
        GuestLocation = string.IsNullOrWhiteSpace(guestLocation) ? null : guestLocation.Trim();
        Body = body.Trim();
        Source = source.Trim();
        StayedOn = stayedOn;
    }

    public void SetPublished(bool isPublished) => IsPublished = isPublished;

    /// <summary>Star rating, 1–5.</summary>
    public int Rating { get; private set; }

    public string GuestFirstName { get; private set; } = null!;

    public string? GuestLocation { get; private set; }

    public string Body { get; private set; } = null!;

    /// <summary>Origin of the review ("Airbnb" or "Direct").</summary>
    public string Source { get; private set; } = null!;

    public DateOnly StayedOn { get; private set; }

    public bool IsPublished { get; private set; }
}
