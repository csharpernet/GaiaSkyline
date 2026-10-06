using System.Globalization;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// The commission + payout lifecycle (Stage 8 Part A, ADRs 0020/0021). Runs as Hangfire jobs — daily for the
/// lifecycle pass, monthly on the 5th for payouts — and both runs are idempotent so the owner can also
/// trigger them on demand.
/// </summary>
internal sealed partial class PartnerCommissionService(
    AppDbContext dbContext,
    IPartnerAttributionService attribution,
    IPartnerStatementPdfService statements,
    IEmailSender emailSender,
    ISiteSettings settings,
    TimeProvider clock,
    ILogger<PartnerCommissionService> logger) : IPartnerCommissionService
{
    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to email the payout statement for {PayoutId}.")]
    private static partial void LogStatementEmailFailed(ILogger logger, Exception exception, Guid payoutId);

    private const decimal DefaultMinPayoutEur = 50m;
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public async Task RunDailyAsync(CancellationToken cancellationToken)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Lisbon));

        // 1. Self-heal: any confirmed attributed booking that missed the confirmation-time hook.
        var missing = await dbContext.PartnerAttributions.AsNoTracking()
            .Where(a => !dbContext.Commissions.Any(c => c.BookingId == a.BookingId))
            .Select(a => a.BookingId)
            .ToListAsync(cancellationToken);
        foreach (var bookingId in missing)
        {
            await attribution.OnBookingConfirmedAsync(bookingId.Value, cancellationToken);
        }

        // 2..4 operate on unpaid commissions joined to their bookings (small sets).
        var open = await (
            from c in dbContext.Commissions
            join b in dbContext.Bookings on c.BookingId equals b.Id
            where c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Payable
            select new { Commission = c, Booking = b })
            .ToListAsync(cancellationToken);

        foreach (var row in open)
        {
            // Cancelled or fully refunded → nothing is owed (ADR 0020).
            if (row.Booking.Status is BookingStatus.Cancelled or BookingStatus.Refunded)
            {
                row.Commission.Void();
                continue;
            }

            // A partial refund shrinks the basis while the commission is unpaid.
            var basis = row.Booking.Total - row.Booking.TouristTax - row.Booking.CleaningFee - row.Booking.RefundedAmount;
            if (basis != row.Commission.BasisAmount)
            {
                row.Commission.Recalculate(basis);
            }

            // Pending → Payable 30 days after check-out.
            if (row.Commission.Status == CommissionStatus.Pending
                && row.Booking.CheckOut.AddDays(30) <= today)
            {
                row.Commission.MakePayable();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RunPayoutsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var nowLisbon = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Lisbon);
        var period = nowLisbon.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var minimum = ReadMinimumPayout();

        var payableByPartner = await dbContext.Commissions
            .Where(c => c.Status == CommissionStatus.Payable)
            .GroupBy(c => c.PartnerId)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var partnerId in payableByPartner)
        {
            if (await dbContext.Payouts.AsNoTracking()
                    .AnyAsync(p => p.PartnerId == partnerId && p.PeriodLabel == period, cancellationToken))
            {
                continue; // idempotent per partner + period (ADR 0021)
            }

            var commissions = await dbContext.Commissions
                .Where(c => c.PartnerId == partnerId && c.Status == CommissionStatus.Payable)
                .ToListAsync(cancellationToken);
            var total = commissions.Aggregate(Money.Zero("EUR"), (sum, c) => sum + c.Amount);
            if (total.Amount < minimum)
            {
                continue; // below the minimum payout — carries over (ADR 0021)
            }

            var payout = new Payout(PayoutId.New(), partnerId, period, total, nowUtc);
            dbContext.Payouts.Add(payout);
            foreach (var commission in commissions)
            {
                commission.AssignToPayout(payout.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            created++;

            await SendStatementAsync(payout, cancellationToken);
        }

        return created;
    }

    private async Task SendStatementAsync(Payout payout, CancellationToken cancellationToken)
    {
        var payoutPartnerId = payout.PartnerId;
        var partner = await dbContext.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == payoutPartnerId, cancellationToken);
        if (partner is null)
        {
            return;
        }

        try
        {
            var pdf = await statements.GenerateAsync(payout.Id.Value, cancellationToken);
            var attachments = pdf is null
                ? null
                : new[] { new EmailAttachment($"gaia-skyline-payout-{payout.PeriodLabel}.pdf", "application/pdf", pdf) };
            await emailSender.SendAsync(
                new EmailMessage(
                    partner.Email,
                    partner.Name,
                    $"Gaia Skyline — partner payout statement {payout.PeriodLabel}",
                    $"<p>Olá {partner.Name},</p><p>Your payout for <strong>{payout.PeriodLabel}</strong> is "
                    + $"<strong>€{payout.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture)}</strong>. "
                    + "The statement is attached; the transfer goes to the IBAN on file within a few days.</p>"
                    + "<p>Thank you for sending guests our way!</p>",
                    attachments),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The payout stands even when the email fails — the statement stays downloadable in the admin.
            LogStatementEmailFailed(logger, ex, payout.Id.Value);
        }
    }

    private decimal ReadMinimumPayout()
    {
        var raw = settings.Get(SettingKeys.PartnerMinPayoutEur);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : DefaultMinPayoutEur;
    }
}
