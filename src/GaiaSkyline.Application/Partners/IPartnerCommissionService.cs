namespace GaiaSkyline.Application.Partners;

/// <summary>
/// The commission + payout lifecycle jobs (Stage 8 Part A, ADRs 0020/0021). Daily: create any missing
/// commissions for confirmed attributed bookings (self-healing), move Pending → Payable 30 days after
/// check-out, void cancelled/fully-refunded bookings' commissions and recalculate partially-refunded ones.
/// Monthly (the 5th): group Payable amounts per partner above the minimum payout, issue the PDF statement
/// and email it; the owner transfers the money manually and marks the payout settled.
/// </summary>
public interface IPartnerCommissionService
{
    /// <summary>The daily lifecycle pass. Idempotent.</summary>
    Task RunDailyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The monthly payout run for the previous calendar month (or an on-demand run from the admin).
    /// Idempotent per partner + period. Returns how many payouts were created.
    /// </summary>
    Task<int> RunPayoutsAsync(CancellationToken cancellationToken);
}

/// <summary>Builds the PDF payout statement a partner receives with each payout (ADR 0021).</summary>
public interface IPartnerStatementPdfService
{
    Task<byte[]?> GenerateAsync(Guid payoutId, CancellationToken cancellationToken);
}
