using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>Loads a booking, composes its localized email and sends it. Invoked inline or as a Hangfire job.</summary>
internal sealed class BookingEmailDispatcher(
    AppDbContext dbContext,
    BookingEmailComposer composer,
    IEmailSender emailSender,
    IOptions<PropertyManagerOptions> propertyManagerOptions,
    ILogger<BookingEmailDispatcher> logger) : IBookingEmailDispatcher
{
    public async Task DispatchAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId.From(bookingId), cancellationToken);
        if (booking is null)
        {
            return;
        }

        // The property-manager copy goes to each configured address (or nowhere, silently, when unset).
        if (kind == BookingEmailKind.PropertyManager)
        {
            var addresses = propertyManagerOptions.Value.NotificationEmails;
            if (addresses.Count == 0)
            {
                logger.LogInformation("No property-manager emails configured; skipping the property-manager notification.");
                return;
            }

            var pmMessage = await composer.ComposeAsync(booking, kind, cancellationToken);
            foreach (var address in addresses)
            {
                await emailSender.SendAsync(pmMessage with { ToAddress = address, ToName = address }, cancellationToken);
            }

            return;
        }

        var message = await composer.ComposeAsync(booking, kind, cancellationToken);
        await emailSender.SendAsync(message, cancellationToken);
    }
}
