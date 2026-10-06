using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// Owner-side partner management (Stage 8 Part A): list/detail with earnings, percentage + code edits,
/// suspend/reactivate (which also stops the code from discounting), the commissions ledger, payouts
/// administration and the clicks report.
/// </summary>
internal sealed class PartnersAdminService(
    AppDbContext dbContext,
    IContentRevision revision,
    TimeProvider clock) : IPartnersAdminService
{
    public async Task<IReadOnlyList<PartnerListItemDto>> GetPartnersAsync(CancellationToken cancellationToken)
    {
        var partners = await dbContext.Partners.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
        var codes = await PromoCodesByPartnerAsync(cancellationToken);

        // Money sums happen in memory — the converted Money column does not aggregate in SQL, and the
        // commissions table stays small for a single property.
        var amounts = await dbContext.Commissions.AsNoTracking()
            .Select(c => new { c.PartnerId, c.Status, c.Amount })
            .ToListAsync(cancellationToken);

        decimal SumFor(PartnerId id, CommissionStatus status) =>
            amounts.Where(s => s.PartnerId == id && s.Status == status).Sum(s => s.Amount.Amount);

        return partners.Select(p => new PartnerListItemDto(
            p.Id.Value,
            p.Name,
            p.Email,
            codes.GetValueOrDefault(p.Id, string.Empty),
            p.Status.ToString(),
            p.GuestDiscountPct,
            p.CommissionPct,
            SumFor(p.Id, CommissionStatus.Pending),
            SumFor(p.Id, CommissionStatus.Payable),
            SumFor(p.Id, CommissionStatus.Paid))).ToList();
    }

    public async Task<PartnerAdminDetailDto?> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken)
    {
        var id = PartnerId.From(partnerId);
        var partner = await dbContext.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partner is null)
        {
            return null;
        }

        var codes = await PromoCodesByPartnerAsync(cancellationToken);
        var commissions = await CommissionRowsAsync(id, cancellationToken);
        var payoutRows = await dbContext.Payouts.AsNoTracking()
            .Where(p => p.PartnerId == id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new PartnerAdminDetailDto(
            partner.Id.Value,
            partner.Name,
            partner.Email,
            codes.GetValueOrDefault(partner.Id, string.Empty),
            partner.Status.ToString(),
            partner.GuestDiscountPct,
            partner.CommissionPct,
            partner.TermsVersion,
            partner.TermsAcceptedAtUtc,
            partner.PayoutIban is { Length: >= 4 } iban ? $"•••• {iban[^4..]}" : null,
            partner.PayoutAccountHolder,
            partner.PayoutTaxId,
            partner.PayoutCountry,
            partner.CreatedAtUtc,
            partner.ActivatedAtUtc,
            commissions,
            payoutRows.Select(p => new PayoutAdminRowDto(
                p.Id.Value, partner.Id.Value, partner.Name, p.PeriodLabel, p.Amount.Amount,
                p.Status.ToString(), p.CreatedAtUtc, p.SettledAtUtc)).ToList());
    }

    public async Task<PartnerAdminResult> UpdatePartnerAsync(
        Guid partnerId, int guestDiscountPct, int commissionPct, string promoCode, CancellationToken cancellationToken)
    {
        var id = PartnerId.From(partnerId);
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partner is null)
        {
            return PartnerAdminResult.Fail("Partner not found.");
        }

        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return PartnerAdminResult.Fail("The promo code cannot be blank.");
        }

        try
        {
            partner.SetPercentages(guestDiscountPct, commissionPct);
        }
        catch (ArgumentOutOfRangeException)
        {
            return PartnerAdminResult.Fail("Percentages must be between 0 and 100.");
        }

        if (partner.PromoCodeId is { } promoId)
        {
            var promo = await dbContext.PromoCodes.FirstAsync(p => p.Id == promoId, cancellationToken);
            var normalized = promoCode.Trim().ToUpperInvariant();
            if (!string.Equals(promo.Code, normalized, StringComparison.Ordinal))
            {
                if (await dbContext.PromoCodes.AsNoTracking()
                        .AnyAsync(p => p.Code == normalized && p.Id != promoId, cancellationToken))
                {
                    return PartnerAdminResult.Fail($"The code {normalized} is already taken.");
                }

                promo.Rename(normalized);
            }

            // The guest discount lives on the promo code — keep it in step with the partner's percentage.
            promo.Update(
                Math.Max(1, partner.GuestDiscountPct), promo.IsActive, promo.ValidFrom, promo.ValidUntil);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump(); // the public /partners pages mention the discount
        return PartnerAdminResult.Success();
    }

    public async Task<PartnerAdminResult> SuspendAsync(Guid partnerId, CancellationToken cancellationToken) =>
        await SetSuspendedAsync(partnerId, suspended: true, cancellationToken);

    public async Task<PartnerAdminResult> ReactivateAsync(Guid partnerId, CancellationToken cancellationToken) =>
        await SetSuspendedAsync(partnerId, suspended: false, cancellationToken);

    public async Task<IReadOnlyList<CommissionRowDto>> GetLedgerAsync(CancellationToken cancellationToken) =>
        await CommissionRowsAsync(partnerId: null, cancellationToken);

    public async Task<IReadOnlyList<PayoutAdminRowDto>> GetPayoutsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from p in dbContext.Payouts
            join partner in dbContext.Partners on p.PartnerId equals partner.Id
            orderby p.CreatedAtUtc descending
            select new { Payout = p, partner.Name })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PayoutAdminRowDto(
            r.Payout.Id.Value, r.Payout.PartnerId.Value, r.Name, r.Payout.PeriodLabel,
            r.Payout.Amount.Amount, r.Payout.Status.ToString(), r.Payout.CreatedAtUtc, r.Payout.SettledAtUtc)).ToList();
    }

    public async Task<PartnerAdminResult> MarkPayoutSettledAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var id = PayoutId.From(payoutId);
        var payout = await dbContext.Payouts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payout is null)
        {
            return PartnerAdminResult.Fail("Payout not found.");
        }

        try
        {
            payout.MarkSettled(clock.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            return PartnerAdminResult.Fail(ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return PartnerAdminResult.Success();
    }

    public async Task<IReadOnlyList<PartnerClicksReportRow>> GetClicksReportAsync(CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var clicks = await dbContext.PartnerClicks.AsNoTracking()
            .Where(c => c.UtcAt >= since)
            .Select(c => new { c.PartnerId, c.UtcAt, c.AnonymousId })
            .ToListAsync(cancellationToken);
        var names = await dbContext.Partners.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return clicks
            .GroupBy(c => new { c.PartnerId, Day = DateOnly.FromDateTime(c.UtcAt) })
            .Select(g => new PartnerClicksReportRow(
                g.Key.PartnerId.Value,
                names.GetValueOrDefault(g.Key.PartnerId, "(unknown)"),
                g.Key.Day,
                g.Count(),
                g.Select(c => c.AnonymousId).Distinct().Count()))
            .OrderByDescending(r => r.Day)
            .ThenBy(r => r.PartnerName, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<PartnerAdminResult> SetSuspendedAsync(
        Guid partnerId, bool suspended, CancellationToken cancellationToken)
    {
        var id = PartnerId.From(partnerId);
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partner is null)
        {
            return PartnerAdminResult.Fail("Partner not found.");
        }

        try
        {
            if (suspended)
            {
                partner.Suspend();
            }
            else
            {
                partner.Reactivate();
            }
        }
        catch (InvalidOperationException ex)
        {
            return PartnerAdminResult.Fail(ex.Message);
        }

        // The code stops (or resumes) discounting with the partner (ADR 0019).
        if (partner.PromoCodeId is { } promoId)
        {
            var promo = await dbContext.PromoCodes.FirstAsync(p => p.Id == promoId, cancellationToken);
            promo.Update(promo.DiscountPct, isActive: !suspended, promo.ValidFrom, promo.ValidUntil);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return PartnerAdminResult.Success();
    }

    private async Task<IReadOnlyList<CommissionRowDto>> CommissionRowsAsync(
        PartnerId? partnerId, CancellationToken cancellationToken)
    {
        var query =
            from c in dbContext.Commissions
            join b in dbContext.Bookings on c.BookingId equals b.Id
            join p in dbContext.Partners on c.PartnerId equals p.Id
            select new { Commission = c, b.ReferenceCode, b.CheckIn, b.CheckOut, PartnerName = p.Name };
        if (partnerId is { } id)
        {
            query = query.Where(r => r.Commission.PartnerId == id);
        }

        var rows = await query
            .AsNoTracking()
            .OrderByDescending(r => r.Commission.CreatedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CommissionRowDto(
            r.Commission.Id.Value,
            r.Commission.PartnerId.Value,
            r.PartnerName,
            r.ReferenceCode,
            r.CheckIn,
            r.CheckOut,
            r.Commission.BasisAmount.Amount,
            r.Commission.Pct,
            r.Commission.Amount.Amount,
            r.Commission.Status.ToString(),
            r.Commission.CreatedAtUtc)).ToList();
    }

    private async Task<Dictionary<PartnerId, string>> PromoCodesByPartnerAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PromoCodes.AsNoTracking()
            .Where(p => p.PartnerId != null)
            .Select(p => new { p.PartnerId, p.Code })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.PartnerId!.Value, r => r.Code);
    }
}
