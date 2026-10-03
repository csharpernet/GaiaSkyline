using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// The unpaid-hold safety net (Hangfire, every 5 minutes): card/wallet bookings held longer than the
/// unpaid-hold window are released; Multibanco bookings are released once past their voucher expiry.
/// This runs in addition to webhook handling, so dates are freed even if a webhook is missed.
/// </summary>
internal sealed class BookingExpiryService(
    AppDbContext dbContext,
    IBookingLifecycleService lifecycle,
    IOptions<StripeOptions> options,
    TimeProvider clock) : IBookingExpiryService
{
    private readonly StripeOptions _options = options.Value;

    public async Task<int> ExpireUnpaidHoldsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var cardCutoffUtc = nowUtc.AddMinutes(-_options.UnpaidHoldMinutes);

        var expired = await dbContext.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.AwaitingPayment)
            .Where(b =>
                (b.MultibancoReference == null && b.CreatedAtUtc < cardCutoffUtc) ||
                (b.MultibancoReference != null && b.PaymentExpiresAtUtc != null && b.PaymentExpiresAtUtc < nowUtc))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var id in expired)
        {
            try
            {
                await lifecycle.CancelAndReleaseAsync(id, "Unpaid hold expired", cancellationToken);
                released++;
            }
            catch (InvalidBookingStatusTransitionException)
            {
                // Confirmed/cancelled between the scan and now; nothing to release.
            }
        }

        return released;
    }
}
