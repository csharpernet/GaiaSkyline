namespace GaiaSkyline.Application.Partners;

/// <summary>One side of the this-month-vs-last comparison on the partner dashboard.</summary>
public sealed record PartnerPeriodStats(
    int Clicks,
    int Bookings,
    decimal BookingValueEur,
    decimal CommissionEur);

/// <summary>A booking row on the partner dashboard — guest surname is never exposed (Stage 8 Part A).</summary>
public sealed record PartnerBookingRow(
    DateOnly CheckIn,
    string GuestFirstName,
    int Nights,
    decimal ValueEur,
    decimal CommissionEur,
    string CommissionStatus);

public sealed record PartnerPayoutRow(
    Guid PayoutId,
    string PeriodLabel,
    decimal AmountEur,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SettledAtUtc);

/// <summary>Everything the partner dashboard renders.</summary>
public sealed record PartnerDashboardDto(
    Guid PartnerId,
    string Name,
    string PromoCode,
    int GuestDiscountPct,
    int CommissionPct,
    string Status,
    PartnerPeriodStats ThisMonth,
    PartnerPeriodStats LastMonth,
    IReadOnlyList<PartnerBookingRow> Bookings,
    IReadOnlyList<PartnerPayoutRow> Payouts,
    string? PayoutIbanMasked,
    string? PayoutAccountHolder,
    string? PayoutTaxId,
    string? PayoutCountry);

/// <summary>Reads for the partner dashboard and the partner API (Stage 8 Part A).</summary>
public interface IPartnerDashboardService
{
    /// <summary>The dashboard for the partner linked to this Identity user; null when none.</summary>
    Task<PartnerDashboardDto?> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stats for an arbitrary period (the partner API's <c>?period=</c>).</summary>
    Task<PartnerPeriodStats?> GetStatsAsync(
        Guid userId, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken);

    /// <summary>Booking rows within a window (the partner API's <c>/bookings?from=&amp;to=</c>).</summary>
    Task<IReadOnlyList<PartnerBookingRow>?> GetBookingsAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<PartnerPayoutRow>?> GetPayoutsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Updates the partner's own payout details (code and percentages stay read-only).</summary>
    Task<PartnerOnboardingResult> UpdatePayoutDetailsAsync(
        Guid userId, string iban, string accountHolder, string taxId, string country, CancellationToken cancellationToken);
}
