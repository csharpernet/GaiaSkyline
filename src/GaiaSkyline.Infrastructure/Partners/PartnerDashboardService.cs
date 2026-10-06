using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// Reads for the partner dashboard and the partner API (Stage 8 Part A). Everything is keyed by the
/// signed-in Identity user; guests' surnames never leave this service.
/// </summary>
internal sealed class PartnerDashboardService(AppDbContext dbContext, TimeProvider clock) : IPartnerDashboardService
{
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public async Task<PartnerDashboardDto?> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var partner = await PartnerOfAsync(userId, cancellationToken);
        if (partner is null)
        {
            return null;
        }

        var todayLisbon = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, Lisbon);
        var monthStart = new DateOnly(todayLisbon.Year, todayLisbon.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);
        var nextMonthStart = monthStart.AddMonths(1);

        var thisMonth = await StatsForAsync(partner, monthStart, nextMonthStart, cancellationToken);
        var lastMonth = await StatsForAsync(partner, lastMonthStart, monthStart, cancellationToken);
        var bookings = await BookingRowsAsync(partner, from: null, to: null, cancellationToken);
        var payouts = await PayoutRowsAsync(partner, cancellationToken);

        var promoId = partner.PromoCodeId;
        var code = promoId is null
            ? string.Empty
            : await dbContext.PromoCodes.AsNoTracking()
                .Where(p => p.Id == promoId).Select(p => p.Code).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new PartnerDashboardDto(
            partner.Id.Value,
            partner.Name,
            code,
            partner.GuestDiscountPct,
            partner.CommissionPct,
            partner.Status.ToString(),
            thisMonth,
            lastMonth,
            bookings,
            payouts,
            MaskIban(partner.PayoutIban),
            partner.PayoutAccountHolder,
            partner.PayoutTaxId,
            partner.PayoutCountry);
    }

    public async Task<PartnerPeriodStats?> GetStatsAsync(
        Guid userId, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var partner = await PartnerOfAsync(userId, cancellationToken);
        return partner is null ? null : await StatsForAsync(partner, from, toExclusive, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerBookingRow>?> GetBookingsAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var partner = await PartnerOfAsync(userId, cancellationToken);
        return partner is null ? null : await BookingRowsAsync(partner, from, to, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerPayoutRow>?> GetPayoutsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var partner = await PartnerOfAsync(userId, cancellationToken);
        return partner is null ? null : await PayoutRowsAsync(partner, cancellationToken);
    }

    public async Task<PartnerOnboardingResult> UpdatePayoutDetailsAsync(
        Guid userId, string iban, string accountHolder, string taxId, string country, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (partner is null)
        {
            return PartnerOnboardingResult.Fail("No partner profile is linked to this account.");
        }

        try
        {
            partner.SetPayoutDetails(iban, accountHolder, taxId, country);
        }
        catch (ArgumentException ex)
        {
            return PartnerOnboardingResult.Fail(ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return PartnerOnboardingResult.Success();
    }

    private async Task<Partner?> PartnerOfAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    private async Task<PartnerPeriodStats> StatsForAsync(
        Partner partner, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var partnerId = partner.Id;
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var clicks = await dbContext.PartnerClicks.AsNoTracking()
            .CountAsync(c => c.PartnerId == partnerId && c.UtcAt >= fromUtc && c.UtcAt < toUtc, cancellationToken);

        var rows = await (
            from a in dbContext.PartnerAttributions
            join b in dbContext.Bookings on a.BookingId equals b.Id
            where a.PartnerId == partnerId
                && b.CreatedAtUtc >= fromUtc && b.CreatedAtUtc < toUtc
                && b.Status != BookingStatus.Cancelled
            join c in dbContext.Commissions on b.Id equals c.BookingId into commissions
            from c in commissions.DefaultIfEmpty()
            select new { b.Total, Commission = (Money?)c!.Amount })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PartnerPeriodStats(
            clicks,
            rows.Count,
            rows.Sum(r => r.Total.Amount),
            rows.Sum(r => r.Commission?.Amount ?? 0m));
    }

    private async Task<IReadOnlyList<PartnerBookingRow>> BookingRowsAsync(
        Partner partner, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var partnerId = partner.Id;
        var query =
            from a in dbContext.PartnerAttributions
            join b in dbContext.Bookings on a.BookingId equals b.Id
            where a.PartnerId == partnerId
            join c in dbContext.Commissions on b.Id equals c.BookingId into commissions
            from c in commissions.DefaultIfEmpty()
            select new
            {
                b.CheckIn,
                b.CheckOut,
                b.GuestName,
                b.Total,
                CommissionAmount = (Money?)c!.Amount,
                CommissionStatus = (CommissionStatus?)c.Status,
            };

        if (from is { } f)
        {
            query = query.Where(r => r.CheckIn >= f);
        }

        if (to is { } t)
        {
            query = query.Where(r => r.CheckIn <= t);
        }

        var rows = await query
            .AsNoTracking()
            .OrderByDescending(r => r.CheckIn)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PartnerBookingRow(
            r.CheckIn,
            FirstName(r.GuestName),
            r.CheckOut.DayNumber - r.CheckIn.DayNumber,
            r.Total.Amount,
            r.CommissionAmount?.Amount ?? 0m,
            r.CommissionStatus?.ToString() ?? "—")).ToList();
    }

    private async Task<IReadOnlyList<PartnerPayoutRow>> PayoutRowsAsync(
        Partner partner, CancellationToken cancellationToken)
    {
        var partnerId = partner.Id;
        var payouts = await dbContext.Payouts.AsNoTracking()
            .Where(p => p.PartnerId == partnerId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return payouts.Select(p => new PartnerPayoutRow(
            p.Id.Value, p.PeriodLabel, p.Amount.Amount, p.Status.ToString(), p.CreatedAtUtc, p.SettledAtUtc)).ToList();
    }

    /// <summary>Only the guest's first name reaches a partner (Stage 8 Part A).</summary>
    private static string FirstName(string guestName)
    {
        var first = guestName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Guest" : first;
    }

    private static string? MaskIban(string? iban) =>
        iban is null || iban.Length < 4 ? iban : $"•••• {iban[^4..]}";
}
