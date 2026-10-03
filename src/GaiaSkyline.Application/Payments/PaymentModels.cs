namespace GaiaSkyline.Application.Payments;

/// <summary>The result of creating a PaymentIntent for a booking.</summary>
public sealed record PaymentIntentResult(
    string PaymentIntentId,
    string ClientSecret,
    string CustomerId);

/// <summary>Decides whether Multibanco may be offered for a given check-in (needs enough lead time).</summary>
public static class MultibancoPolicy
{
    /// <summary>True if check-in is at least <paramref name="minLeadDays"/> days after today.</summary>
    public static bool IsAllowed(DateOnly checkIn, DateOnly today, int minLeadDays) =>
        checkIn.DayNumber - today.DayNumber >= minLeadDays;
}
