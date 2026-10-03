using GaiaSkyline.Domain.Availability;

namespace GaiaSkyline.Application.Availability;

public sealed record OwnerBlockDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    OwnerBlockKind Kind,
    string? Note,
    DateTime CreatedAtUtc,
    string CreatedBy);

/// <summary>
/// Owner-entered calendar blocks for manual sync. Creating a block that overlaps an active direct
/// booking throws <see cref="OwnerBlockConflictsWithBookingException"/>. Mutations invalidate the
/// availability and ICS caches.
/// </summary>
public interface IOwnerBlockService
{
    Task<Guid> CreateAsync(
        DateOnly startDate, DateOnly endDateExclusive, OwnerBlockKind kind, string? note, string createdBy, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OwnerBlockDto>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// ExternalBooking owner blocks whose dates exactly match an imported external-calendar block —
    /// i.e. manual copies that can be deleted once iCal sync is connected.
    /// </summary>
    Task<IReadOnlyList<OwnerBlockDto>> ListImportedDuplicatesAsync(CancellationToken cancellationToken);
}
