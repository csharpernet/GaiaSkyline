using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Data;

/// <summary>
/// Idempotently seeds the initial pricing rule, fees and cancellation policy from
/// <see cref="BookingPricingOptions"/>. After seeding these are backend-managed rows (Stage 7 admin);
/// discounts and promo codes are intentionally seeded empty. See ADR 0009.
/// </summary>
public sealed class BookingSeeder(AppDbContext dbContext, IOptions<BookingPricingOptions> options)
{
    private const string Currency = "EUR";

    // A broad standing rule; the owner narrows/adds seasons in the backend.
    private static readonly DateOnly RuleStart = new(2025, 1, 1);
    private static readonly DateOnly RuleEnd = new(2035, 12, 31);

    private readonly BookingPricingOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await EnsurePricingRuleAsync(cancellationToken);
        await EnsureFeesAsync(cancellationToken);
        await EnsureCancellationPolicyAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePricingRuleAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.PricingRules.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.PricingRules.Add(new PricingRule(
            PricingRuleId.From(DeterministicGuid.From("pricing:base")),
            RuleStart,
            RuleEnd,
            new Money(_options.BaseNightlyRateEur, Currency),
            _options.StandardMinNights,
            _options.WeeklyDiscountPct,
            _options.MonthlyDiscountPct));
    }

    private async Task EnsureFeesAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Fees.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Fees.Add(new Fee(
            FeeId.From(DeterministicGuid.From("fee:cleaning")),
            FeeType.Cleaning,
            new Money(_options.CleaningFeeEur, Currency),
            isPerNight: false,
            isPerGuest: false));

        if (_options.TouristTaxPerAdultPerNightEur is { } taxAmount && taxAmount > 0)
        {
            dbContext.Fees.Add(new Fee(
                FeeId.From(DeterministicGuid.From("fee:tourist-tax")),
                FeeType.TouristTax,
                new Money(taxAmount, Currency),
                isPerNight: true,
                isPerGuest: true,
                maxNights: _options.TouristTaxMaxNights,
                minAgeExempt: _options.TouristTaxMinAgeExempt));
        }
    }

    private async Task EnsureCancellationPolicyAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.CancellationPolicies.AnyAsync(cancellationToken))
        {
            return;
        }

        var tiers = _options.CancellationTiers
            .Select(t => new CancellationTier(t.DaysBeforeCheckIn, t.RefundPct))
            .ToList();

        dbContext.CancellationPolicies.Add(new CancellationPolicy(
            CancellationPolicyId.From(DeterministicGuid.From("cancellation:policy")),
            tiers));
    }
}
