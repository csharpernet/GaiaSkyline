namespace GaiaSkyline.Application.Partners;

/// <summary>A row in the admin partners list (Stage 8 Part A).</summary>
public sealed record PartnerListItemDto(
    Guid Id,
    string Name,
    string Email,
    string PromoCode,
    string Status,
    int GuestDiscountPct,
    int CommissionPct,
    decimal PendingEur,
    decimal PayableEur,
    decimal PaidEur);

/// <summary>A commission row in the admin ledger and on the partner detail page.</summary>
public sealed record CommissionRowDto(
    Guid Id,
    Guid PartnerId,
    string PartnerName,
    string BookingReference,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal BasisEur,
    int Pct,
    decimal AmountEur,
    string Status,
    DateTime CreatedAtUtc);

public sealed record PayoutAdminRowDto(
    Guid Id,
    Guid PartnerId,
    string PartnerName,
    string PeriodLabel,
    decimal AmountEur,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SettledAtUtc);

/// <summary>Daily click counts for the admin clicks report.</summary>
public sealed record PartnerClicksReportRow(Guid PartnerId, string PartnerName, DateOnly Day, int Clicks, int UniqueVisitors);

/// <summary>Full partner detail for the admin page.</summary>
public sealed record PartnerAdminDetailDto(
    Guid Id,
    string Name,
    string Email,
    string PromoCode,
    string Status,
    int GuestDiscountPct,
    int CommissionPct,
    string? TermsVersion,
    DateTime? TermsAcceptedAtUtc,
    string? PayoutIbanMasked,
    string? PayoutAccountHolder,
    string? PayoutTaxId,
    string? PayoutCountry,
    DateTime CreatedAtUtc,
    DateTime? ActivatedAtUtc,
    IReadOnlyList<CommissionRowDto> Commissions,
    IReadOnlyList<PayoutAdminRowDto> Payouts);

public sealed record PartnerAdminResult(bool Ok, string? Error)
{
    public static PartnerAdminResult Success() => new(true, null);

    public static PartnerAdminResult Fail(string error) => new(false, error);
}

/// <summary>Owner-side management of the partner program (Stage 8 Part A).</summary>
public interface IPartnersAdminService
{
    Task<IReadOnlyList<PartnerListItemDto>> GetPartnersAsync(CancellationToken cancellationToken);

    Task<PartnerAdminDetailDto?> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken);

    /// <summary>Edits the partner's percentages and/or promo code (code stays unique, uppercase).</summary>
    Task<PartnerAdminResult> UpdatePartnerAsync(
        Guid partnerId, int guestDiscountPct, int commissionPct, string promoCode, CancellationToken cancellationToken);

    Task<PartnerAdminResult> SuspendAsync(Guid partnerId, CancellationToken cancellationToken);

    Task<PartnerAdminResult> ReactivateAsync(Guid partnerId, CancellationToken cancellationToken);

    /// <summary>The full commissions ledger, newest first.</summary>
    Task<IReadOnlyList<CommissionRowDto>> GetLedgerAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PayoutAdminRowDto>> GetPayoutsAsync(CancellationToken cancellationToken);

    Task<PartnerAdminResult> MarkPayoutSettledAsync(Guid payoutId, CancellationToken cancellationToken);

    /// <summary>Daily clicks per partner over the last 30 days.</summary>
    Task<IReadOnlyList<PartnerClicksReportRow>> GetClicksReportAsync(CancellationToken cancellationToken);
}
