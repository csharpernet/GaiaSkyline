using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Availability;

internal sealed class OwnerBlockService(
    AppDbContext dbContext,
    IAvailabilityService availabilityService,
    IIcsCacheInvalidator icsCacheInvalidator,
    TimeProvider clock) : IOwnerBlockService
{
    private static readonly BookingStatus[] ActiveStatuses =
        [BookingStatus.AwaitingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<Guid> CreateAsync(
        DateOnly startDate, DateOnly endDateExclusive, OwnerBlockKind kind, string? note, string createdBy, CancellationToken cancellationToken)
    {
        await ThrowIfOverlapsActiveBookingAsync(startDate, endDateExclusive, cancellationToken);

        var block = new OwnerBlock(
            OwnerBlockId.New(), startDate, endDateExclusive, kind, note, clock.GetUtcNow().UtcDateTime, createdBy);
        dbContext.OwnerBlocks.Add(block);
        await dbContext.SaveChangesAsync(cancellationToken);

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();
        return block.Id.Value;
    }

    public async Task<bool> UpdateAsync(
        Guid id, DateOnly startDate, DateOnly endDateExclusive, string? note, CancellationToken cancellationToken)
    {
        var block = await dbContext.OwnerBlocks.FirstOrDefaultAsync(b => b.Id == OwnerBlockId.From(id), cancellationToken);
        if (block is null)
        {
            return false;
        }

        await ThrowIfOverlapsActiveBookingAsync(startDate, endDateExclusive, cancellationToken);

        block.Reschedule(startDate, endDateExclusive, note);
        await dbContext.SaveChangesAsync(cancellationToken);

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();
        return true;
    }

    /// <summary>Rejects a range that overlaps an active direct booking, naming the booking.</summary>
    private async Task ThrowIfOverlapsActiveBookingAsync(
        DateOnly startDate, DateOnly endDateExclusive, CancellationToken cancellationToken)
    {
        var conflictingReference = await dbContext.Bookings.AsNoTracking()
            .Where(b => ActiveStatuses.Contains(b.Status) && b.CheckIn < endDateExclusive && startDate < b.CheckOut)
            .Select(b => b.ReferenceCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (conflictingReference is not null)
        {
            throw new OwnerBlockConflictsWithBookingException(conflictingReference);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var block = await dbContext.OwnerBlocks.FirstOrDefaultAsync(b => b.Id == OwnerBlockId.From(id), cancellationToken);
        if (block is null)
        {
            return false;
        }

        dbContext.OwnerBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();
        return true;
    }

    public async Task<IReadOnlyList<OwnerBlockDto>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        return await dbContext.OwnerBlocks.AsNoTracking()
            .Where(b => b.StartDate < to && from < b.EndDate)
            .OrderBy(b => b.StartDate)
            .Select(b => new OwnerBlockDto(b.Id.Value, b.StartDate, b.EndDate, b.Kind, b.Note, b.CreatedAtUtc, b.CreatedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerBlockDto>> ListImportedDuplicatesAsync(CancellationToken cancellationToken)
    {
        var externalBookings = await dbContext.OwnerBlocks.AsNoTracking()
            .Where(b => b.Kind == OwnerBlockKind.ExternalBooking)
            .ToListAsync(cancellationToken);
        if (externalBookings.Count == 0)
        {
            return [];
        }

        var imported = await dbContext.ExternalCalendarBlocks.AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync(cancellationToken);
        // ExternalCalendarBlock end is inclusive; OwnerBlock end is exclusive.
        var importedRanges = imported.Select(b => (b.StartDate, b.EndDate)).ToHashSet();

        return externalBookings
            .Where(b => importedRanges.Contains((b.StartDate, b.EndDate.AddDays(-1))))
            .OrderBy(b => b.StartDate)
            .Select(b => new OwnerBlockDto(b.Id.Value, b.StartDate, b.EndDate, b.Kind, b.Note, b.CreatedAtUtc, b.CreatedBy))
            .ToList();
    }
}
