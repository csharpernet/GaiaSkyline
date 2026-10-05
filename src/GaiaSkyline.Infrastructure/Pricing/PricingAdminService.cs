using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>
/// Pricing catalogue writes for the admin (Stage 7 §8). Seasons reject overlaps (the per-night
/// resolver takes the first covering rule, so overlaps would be order-dependent); fee rows are
/// replaced wholesale (no FKs point at them); the cancellation policy is swapped as a fresh row
/// (its tiers live in a JSON column); accepting a rate-sync rejection writes a manual per-date rate
/// through <see cref="IDailyRateService"/>. Every change bumps the content revision.
/// </summary>
internal sealed class PricingAdminService(
    AppDbContext dbContext,
    IDailyRateService dailyRates,
    IContentRevision revision,
    IOptions<BookingPricingOptions> pricingOptions,
    TimeProvider clock) : IPricingAdminService
{
    private const string Currency = "EUR";

    public async Task<PricingAdminResult> CreateSeasonAsync(SeasonWriteModel season, CancellationToken cancellationToken)
    {
        // The natural flow is adding a high season INSIDE the seeded catch-all season: a season that
        // fully contains the new one is carved around it (left/right remainders keep its values), so
        // the no-overlap invariant holds without making the owner split ranges by hand. Partial
        // overlaps and exact duplicates stay rejected — those need an explicit edit.
        var overlaps = await dbContext.PricingRules
            .Where(r => r.StartDate <= season.EndDate && season.StartDate <= r.EndDate)
            .ToListAsync(cancellationToken);
        var containing = overlaps
            .FirstOrDefault(r => r.StartDate <= season.StartDate && r.EndDate >= season.EndDate);
        if (overlaps.Count > 0 && containing is null)
        {
            var first = overlaps.OrderBy(r => r.StartDate).First();
            return PricingAdminResult.Fail(
                $"Overlaps the season {first.StartDate:yyyy-MM-dd} → {first.EndDate:yyyy-MM-dd}. Seasons cannot overlap — adjust its dates first.");
        }

        if (containing is not null && containing.StartDate == season.StartDate && containing.EndDate == season.EndDate)
        {
            return PricingAdminResult.Fail("A season with exactly these dates already exists — edit it instead.");
        }

        try
        {
            if (containing is not null)
            {
                CarveAround(containing, season);
            }

            dbContext.PricingRules.Add(new PricingRule(
                PricingRuleId.New(), season.StartDate, season.EndDate,
                new Money(season.NightlyRateEur, Currency), season.MinNights,
                season.WeeklyDiscountPct, season.MonthlyDiscountPct));
        }
        catch (ArgumentException ex)
        {
            return PricingAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    /// <summary>Shrinks <paramref name="outer"/> to the left remainder and adds the right remainder (same values).</summary>
    private void CarveAround(PricingRule outer, SeasonWriteModel season)
    {
        var hasLeft = outer.StartDate < season.StartDate;
        var hasRight = outer.EndDate > season.EndDate;

        if (hasRight)
        {
            var right = new PricingRule(
                PricingRuleId.New(), season.EndDate.AddDays(1), outer.EndDate,
                outer.NightlyRate, outer.MinNights, outer.WeeklyDiscountPct, outer.MonthlyDiscountPct);
            if (hasLeft)
            {
                dbContext.PricingRules.Add(right);
            }
            else
            {
                // The new season starts exactly at the outer's start: the outer simply becomes the right part.
                outer.Update(right.StartDate, right.EndDate, outer.NightlyRate, outer.MinNights,
                    outer.WeeklyDiscountPct, outer.MonthlyDiscountPct);
                return;
            }
        }

        if (hasLeft)
        {
            outer.Update(outer.StartDate, season.StartDate.AddDays(-1), outer.NightlyRate, outer.MinNights,
                outer.WeeklyDiscountPct, outer.MonthlyDiscountPct);
        }
    }

    public async Task<PricingAdminResult> UpdateSeasonAsync(Guid id, SeasonWriteModel season, CancellationToken cancellationToken)
    {
        var rule = await dbContext.PricingRules
            .FirstOrDefaultAsync(r => r.Id == PricingRuleId.From(id), cancellationToken);
        if (rule is null)
        {
            return PricingAdminResult.Fail("That season no longer exists.");
        }

        var error = await FindSeasonConflictAsync(season, excludeId: rule.Id, cancellationToken);
        if (error is not null)
        {
            return PricingAdminResult.Fail(error);
        }

        try
        {
            rule.Update(
                season.StartDate, season.EndDate, new Money(season.NightlyRateEur, Currency),
                season.MinNights, season.WeeklyDiscountPct, season.MonthlyDiscountPct);
        }
        catch (ArgumentException ex)
        {
            return PricingAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> DeleteSeasonAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await dbContext.PricingRules
            .FirstOrDefaultAsync(r => r.Id == PricingRuleId.From(id), cancellationToken);
        if (rule is null)
        {
            return PricingAdminResult.Fail("That season no longer exists.");
        }

        // Without any season, dates fall back to the configured base rate — refuse to leave the
        // calendar priceless when no base is configured.
        var remaining = await dbContext.PricingRules.CountAsync(cancellationToken) - 1;
        if (remaining == 0 && pricingOptions.Value.BaseNightlyRateEur <= 0)
        {
            return PricingAdminResult.Fail("This is the last season and no base rate is configured — dates would have no price.");
        }

        dbContext.PricingRules.Remove(rule);
        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> UpdateFeesAsync(FeesDto fees, CancellationToken cancellationToken)
    {
        if (fees.CleaningFeeEur < 0 || fees.TouristTaxPerAdultPerNightEur is < 0)
        {
            return PricingAdminResult.Fail("Fees cannot be negative.");
        }

        if (fees.TouristTaxMaxNights is < 1)
        {
            return PricingAdminResult.Fail("The tourist-tax nights cap must be at least 1.");
        }

        // Fee rows have no mutators and nothing references them — replace wholesale.
        var existing = await dbContext.Fees.ToListAsync(cancellationToken);
        var exemptAge = existing.FirstOrDefault(f => f.Type == FeeType.TouristTax)?.MinAgeExempt
            ?? pricingOptions.Value.TouristTaxMinAgeExempt;
        dbContext.Fees.RemoveRange(existing);

        dbContext.Fees.Add(new Fee(
            FeeId.New(), FeeType.Cleaning, new Money(fees.CleaningFeeEur, Currency), isPerNight: false, isPerGuest: false));
        if (fees.TouristTaxPerAdultPerNightEur is { } tax and > 0)
        {
            dbContext.Fees.Add(new Fee(
                FeeId.New(), FeeType.TouristTax, new Money(tax, Currency), isPerNight: true, isPerGuest: true,
                maxNights: fees.TouristTaxMaxNights, minAgeExempt: exemptAge));
        }

        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> ReplacePolicyTiersAsync(IReadOnlyList<CancellationTier> tiers, CancellationToken cancellationToken)
    {
        if (tiers.Count == 0)
        {
            return PricingAdminResult.Fail("Keep at least one cancellation tier.");
        }

        if (tiers.Any(t => t.DaysBeforeCheckIn < 0 || t.RefundPct is < 0 or > 100))
        {
            return PricingAdminResult.Fail("Tiers need days ≥ 0 and a refund between 0 and 100%.");
        }

        if (tiers.GroupBy(t => t.DaysBeforeCheckIn).Any(g => g.Count() > 1))
        {
            return PricingAdminResult.Fail("Two tiers use the same days-before-check-in threshold.");
        }

        // The tiers live in a JSON column (owned collection); swapping the singleton row avoids
        // owned-collection change-tracking edge cases entirely.
        var existing = await dbContext.CancellationPolicies.ToListAsync(cancellationToken);
        dbContext.CancellationPolicies.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.CancellationPolicies.Add(new CancellationPolicy(CancellationPolicyId.New(), tiers));
        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> CreatePromoAsync(
        string code, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return PricingAdminResult.Fail("The promo code cannot be empty.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (await dbContext.PromoCodes.AnyAsync(p => p.Code == normalized, cancellationToken))
        {
            return PricingAdminResult.Fail($"The code {normalized} already exists.");
        }

        try
        {
            // ArgumentOutOfRangeException derives from ArgumentException, so one catch covers both.
            dbContext.PromoCodes.Add(new PromoCode(PromoCodeId.New(), normalized, discountPct, isActive, validFrom, validUntil));
        }
        catch (ArgumentException ex)
        {
            return PricingAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> UpdatePromoAsync(
        Guid id, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken)
    {
        var promo = await dbContext.PromoCodes
            .FirstOrDefaultAsync(p => p.Id == PromoCodeId.From(id), cancellationToken);
        if (promo is null)
        {
            return PricingAdminResult.Fail("That promo code no longer exists.");
        }

        try
        {
            promo.Update(discountPct, isActive, validFrom, validUntil);
        }
        catch (ArgumentException ex)
        {
            return PricingAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> DeletePromoAsync(Guid id, CancellationToken cancellationToken)
    {
        var promo = await dbContext.PromoCodes
            .FirstOrDefaultAsync(p => p.Id == PromoCodeId.From(id), cancellationToken);
        if (promo is null)
        {
            return PricingAdminResult.Fail("That promo code no longer exists.");
        }

        dbContext.PromoCodes.Remove(promo);
        await SaveAndBumpAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> AcceptRejectionAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var rejection = await dbContext.RateSyncRejections
            .FirstOrDefaultAsync(r => r.Id == RateSyncRejectionId.From(id), cancellationToken);
        if (rejection is null || rejection.Status != RateSyncRejectionStatus.Open)
        {
            return PricingAdminResult.Fail("That rejection was already settled.");
        }

        // The accepted value becomes an owner (Manual) per-date rate; the bounds only guard imports.
        await dailyRates.SetRangeAsync(
            rejection.Date, rejection.Date, rejection.OfferedPriceEur, rejection.OfferedMinNights, actor, cancellationToken);
        rejection.Accept(clock.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    public async Task<PricingAdminResult> DismissRejectionAsync(Guid id, CancellationToken cancellationToken)
    {
        var rejection = await dbContext.RateSyncRejections
            .FirstOrDefaultAsync(r => r.Id == RateSyncRejectionId.From(id), cancellationToken);
        if (rejection is null || rejection.Status != RateSyncRejectionStatus.Open)
        {
            return PricingAdminResult.Fail("That rejection was already settled.");
        }

        rejection.Dismiss(clock.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
        return PricingAdminResult.Success;
    }

    private async Task<string?> FindSeasonConflictAsync(
        SeasonWriteModel season, PricingRuleId? excludeId, CancellationToken cancellationToken)
    {
        // Closed ranges: [a.Start, a.End] and [b.Start, b.End] overlap when a.Start <= b.End && b.Start <= a.End.
        var overlap = await dbContext.PricingRules.AsNoTracking()
            .Where(r => (excludeId == null || r.Id != excludeId.Value)
                && r.StartDate <= season.EndDate && season.StartDate <= r.EndDate)
            .OrderBy(r => r.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        return overlap is null
            ? null
            : $"Overlaps the season {overlap.StartDate:yyyy-MM-dd} → {overlap.EndDate:yyyy-MM-dd}. Seasons cannot overlap.";
    }

    private async Task SaveAndBumpAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
    }
}
