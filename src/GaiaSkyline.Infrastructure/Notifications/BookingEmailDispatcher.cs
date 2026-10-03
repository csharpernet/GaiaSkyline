using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>Loads a booking, composes its localized email and sends it. Invoked inline or as a Hangfire job.</summary>
internal sealed class BookingEmailDispatcher(
    AppDbContext dbContext,
    BookingEmailComposer composer,
    IEmailSender emailSender) : IBookingEmailDispatcher
{
    public async Task DispatchAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId.From(bookingId), cancellationToken);
        if (booking is null)
        {
            return;
        }

        var message = await composer.ComposeAsync(booking, kind, cancellationToken);
        await emailSender.SendAsync(message, cancellationToken);
    }
}
