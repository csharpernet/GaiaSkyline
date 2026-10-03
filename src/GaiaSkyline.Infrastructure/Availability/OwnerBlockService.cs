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
        // Reject overlap with an active direct booking, naming it.
        var conflictingReference = await dbContext.Bookings.AsNoTracking()
            .Where(b => ActiveStatuses.Contains(b.Status) && b.CheckIn < endDateExclusive && startDate < b.CheckOut)
            .Select(b => b.ReferenceCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (conflictingReference is not null)
        {
            throw new OwnerBlockConflictsWithBookingException(conflictingReference);
        }

        var block = new OwnerBlock(
            OwnerBlockId.New(), startDate, endDateExclusive, kind, note, clock.GetUtcNow().UtcDateTime, createdBy);
        dbContext.OwnerBlocks.Add(block);
        await dbContext.SaveChangesAsync(cancellationToken);

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();
        return block.Id.Value;
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
}
