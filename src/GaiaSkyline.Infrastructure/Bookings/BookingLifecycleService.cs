using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// EF Core implementation of booking state changes that free dates. Cancelling a booking and deleting
/// its occupancy rows happen in the same transaction, so the nights are never left held after a
/// cancellation or an expired payment.
/// </summary>
internal sealed class BookingLifecycleService(
    AppDbContext dbContext,
    IAvailabilityService availabilityService,
    IIcsCacheInvalidator icsCacheInvalidator,
    TimeProvider clock) : IBookingLifecycleService
{
    public async Task CancelAndReleaseAsync(BookingId bookingId, string? reason, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new InvalidOperationException($"Booking {bookingId} was not found.");

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            booking.Cancel(reason, clock.GetUtcNow().UtcDateTime);

            var occupancy = await dbContext.BookingDateOccupancies
                .Where(o => o.BookingId == bookingId)
                .ToListAsync(cancellationToken);
            dbContext.BookingDateOccupancies.RemoveRange(occupancy);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();
    }
}
