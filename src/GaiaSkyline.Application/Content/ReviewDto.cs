namespace GaiaSkyline.Application.Content;

/// <summary>Read model for a published guest review.</summary>
public sealed record ReviewDto(
    Guid Id,
    int Rating,
    string GuestFirstName,
    string? GuestLocation,
    string Body,
    string Source,
    DateOnly StayedOn);
