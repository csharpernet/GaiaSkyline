namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Releases bookings that never got paid. Runs on a Hangfire schedule (every 5 minutes) in addition
/// to webhook handling, so expiry still happens if a webhook is missed: card/wallet holds older than
/// the unpaid-hold window are cancelled; Multibanco holds are cancelled past their voucher expiry.
/// </summary>
public interface IBookingExpiryService
{
    /// <summary>Cancels and releases every booking whose unpaid hold has expired. Returns the count.</summary>
    Task<int> ExpireUnpaidHoldsAsync(CancellationToken cancellationToken);
}
