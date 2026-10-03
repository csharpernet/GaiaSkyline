namespace GaiaSkyline.Domain.Pricing;

/// <summary>
/// One tier of the cancellation policy: cancelling at least <see cref="DaysBeforeCheckIn"/> days
/// before check-in refunds <see cref="RefundPct"/> percent of the total.
/// </summary>
public sealed record CancellationTier(int DaysBeforeCheckIn, int RefundPct);
