using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>The prices admin services (Stage 7 §8): catalogue CRUD, the rates grid and the rejection review.</summary>
public sealed class PricingAdminServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    public PricingAdminServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        // Shared class-fixture database: every test starts from clean pricing tables.
        using var context = _fixture.CreateContext();
        context.DailyRates.ExecuteDelete();
        context.PricingRules.ExecuteDelete();
        context.Fees.ExecuteDelete();
        context.PromoCodes.ExecuteDelete();
        context.CancellationPolicies.ExecuteDelete();
        context.RateSyncRejections.ExecuteDelete();
    }

    private static readonly BookingPricingOptions Options120 = new() { BaseNightlyRateEur = 120m, StandardMinNights = 3 };

    private static PricingAdminService Service(AppDbContext context, BookingPricingOptions? options = null) => new(
        context,
        new DailyRateService(context, new ContentRevision(), new NoAvailability(), TimeProvider.System),
        new ContentRevision(),
        Options.Create(options ?? Options120),
        TimeProvider.System);

    private static PricingAdminReadService Read(AppDbContext context, BookingPricingOptions? options = null) => new(
        context,
        new DailyRateService(context, new ContentRevision(), new NoAvailability(), TimeProvider.System),
        Options.Create(options ?? Options120));

    private static SeasonWriteModel Season(string start, string end, decimal price = 150m, int minNights = 3) => new(
        DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
        DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        price, minNights, 0, 0);

    [Fact]
    public async Task Seasons_reject_overlaps_on_create_and_update_but_allow_touching_ranges()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context);

        (await service.CreateSeasonAsync(Season("2030-06-01", "2030-06-30"), CancellationToken.None)).Ok.Should().BeTrue();
        var overlap = await service.CreateSeasonAsync(Season("2030-06-15", "2030-07-15"), CancellationToken.None);
        overlap.Ok.Should().BeFalse();
        overlap.Error.Should().Contain("Overlaps");

        // Adjacent (closed ranges): July starts the day after June ends — allowed.
        (await service.CreateSeasonAsync(Season("2030-07-01", "2030-07-31"), CancellationToken.None)).Ok.Should().BeTrue();

        // Updating June to collide with July is rejected; updating within itself is fine.
        var june = await context.PricingRules.SingleAsync(r => r.StartDate == new DateOnly(2030, 6, 1));
        (await service.UpdateSeasonAsync(june.Id.Value, Season("2030-06-01", "2030-07-05"), CancellationToken.None))
            .Ok.Should().BeFalse();
        (await service.UpdateSeasonAsync(june.Id.Value, Season("2030-06-05", "2030-06-30", price: 180m), CancellationToken.None))
            .Ok.Should().BeTrue();
        (await context.PricingRules.SingleAsync(r => r.Id == june.Id)).NightlyRate.Amount.Should().Be(180m);
    }

    [Fact]
    public async Task Creating_a_season_inside_a_containing_one_carves_it_into_remainders()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context);
        (await service.CreateSeasonAsync(Season("2035-01-01", "2035-12-31", price: 120m, minNights: 3), CancellationToken.None))
            .Ok.Should().BeTrue();

        // The high season carves the catch-all: left remainder + new season + right remainder.
        (await service.CreateSeasonAsync(Season("2035-07-01", "2035-08-31", price: 200m, minNights: 5), CancellationToken.None))
            .Ok.Should().BeTrue();

        var rules = await context.PricingRules.OrderBy(r => r.StartDate).ToListAsync();
        rules.Should().HaveCount(3);
        rules[0].Should().BeEquivalentTo(new { StartDate = new DateOnly(2035, 1, 1), EndDate = new DateOnly(2035, 6, 30) });
        rules[0].NightlyRate.Amount.Should().Be(120m, "the remainder keeps the outer season's values");
        rules[1].Should().BeEquivalentTo(new { StartDate = new DateOnly(2035, 7, 1), EndDate = new DateOnly(2035, 8, 31), MinNights = 5 });
        rules[1].NightlyRate.Amount.Should().Be(200m);
        rules[2].Should().BeEquivalentTo(new { StartDate = new DateOnly(2035, 9, 1), EndDate = new DateOnly(2035, 12, 31) });
        rules[2].NightlyRate.Amount.Should().Be(120m);

        // An exact-duplicate range must be edited, not re-created.
        (await service.CreateSeasonAsync(Season("2035-07-01", "2035-08-31", price: 150m), CancellationToken.None))
            .Ok.Should().BeFalse();

        // A season starting exactly at a containing season's start shrinks it to the right part only.
        (await service.CreateSeasonAsync(Season("2035-01-01", "2035-02-28", price: 90m), CancellationToken.None))
            .Ok.Should().BeTrue();
        var january = await context.PricingRules.SingleAsync(r => r.StartDate == new DateOnly(2035, 1, 1));
        january.NightlyRate.Amount.Should().Be(90m);
        (await context.PricingRules.SingleAsync(r => r.StartDate == new DateOnly(2035, 3, 1)))
            .EndDate.Should().Be(new DateOnly(2035, 6, 30));
    }

    [Fact]
    public async Task Deleting_the_last_season_requires_a_configured_base_rate()
    {
        await using var context = _fixture.CreateContext();
        var noBase = Service(context, new BookingPricingOptions { BaseNightlyRateEur = 0m });
        (await noBase.CreateSeasonAsync(Season("2031-01-01", "2031-12-31"), CancellationToken.None)).Ok.Should().BeTrue();
        var only = await context.PricingRules.SingleAsync();

        (await noBase.DeleteSeasonAsync(only.Id.Value, CancellationToken.None)).Ok
            .Should().BeFalse("without seasons or a base rate, dates would have no price");

        (await Service(context).DeleteSeasonAsync(only.Id.Value, CancellationToken.None)).Ok
            .Should().BeTrue("a configured base rate can take over");
    }

    [Fact]
    public async Task Fees_upsert_replaces_cleaning_and_optional_tourist_tax()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context);

        (await service.UpdateFeesAsync(new FeesDto(70m, 2m, 7), CancellationToken.None)).Ok.Should().BeTrue();
        var read = Read(context);
        var fees = await read.GetFeesAsync(CancellationToken.None);
        fees.CleaningFeeEur.Should().Be(70m);
        fees.TouristTaxPerAdultPerNightEur.Should().Be(2m);
        fees.TouristTaxMaxNights.Should().Be(7);

        // Blank tax removes the row; cleaning stays.
        (await service.UpdateFeesAsync(new FeesDto(80m, null, null), CancellationToken.None)).Ok.Should().BeTrue();
        fees = await read.GetFeesAsync(CancellationToken.None);
        fees.CleaningFeeEur.Should().Be(80m);
        fees.TouristTaxPerAdultPerNightEur.Should().BeNull();

        (await service.UpdateFeesAsync(new FeesDto(-1m, null, null), CancellationToken.None)).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Policy_tiers_are_replaced_with_validation()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context);

        (await service.ReplacePolicyTiersAsync(
            [new CancellationTier(14, 100), new CancellationTier(7, 50)], CancellationToken.None)).Ok.Should().BeTrue();
        var tiers = await Read(context).GetPolicyTiersAsync(CancellationToken.None);
        tiers.Should().HaveCount(2);
        tiers[0].DaysBeforeCheckIn.Should().Be(14, "ordered most-generous first");

        (await service.ReplacePolicyTiersAsync([], CancellationToken.None)).Ok.Should().BeFalse();
        (await service.ReplacePolicyTiersAsync([new CancellationTier(7, 101)], CancellationToken.None)).Ok.Should().BeFalse();
        (await service.ReplacePolicyTiersAsync(
            [new CancellationTier(7, 10), new CancellationTier(7, 20)], CancellationToken.None)).Ok
            .Should().BeFalse("duplicate thresholds are ambiguous");
    }

    [Fact]
    public async Task Promo_codes_create_normalized_reject_duplicates_and_update()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context);

        (await service.CreatePromoAsync(" summer10 ", 10, true, null, null, CancellationToken.None)).Ok.Should().BeTrue();
        (await service.CreatePromoAsync("SUMMER10", 20, true, null, null, CancellationToken.None)).Ok
            .Should().BeFalse("codes are unique case-insensitively");
        (await service.CreatePromoAsync("BAD", 0, true, null, null, CancellationToken.None)).Ok.Should().BeFalse();

        var promo = (await Read(context).GetPromosAsync(CancellationToken.None)).Single();
        promo.Code.Should().Be("SUMMER10");

        (await service.UpdatePromoAsync(promo.Id, 25, false, new DateOnly(2030, 1, 1), new DateOnly(2030, 6, 30), CancellationToken.None))
            .Ok.Should().BeTrue();
        var updated = (await Read(context).GetPromosAsync(CancellationToken.None)).Single();
        updated.DiscountPct.Should().Be(25);
        updated.IsActive.Should().BeFalse();

        (await service.DeletePromoAsync(promo.Id, CancellationToken.None)).Ok.Should().BeTrue();
        (await Read(context).GetPromosAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Accepting_a_rejection_writes_a_manual_rate_and_settles_it()
    {
        var date = new DateOnly(2032, 5, 10);
        await using var context = _fixture.CreateContext();
        context.RateSyncRejections.Add(new RateSyncRejection(
            RateSyncRejectionId.New(), date, 240m, 2, "PriceLabs", "above ceiling €200", DateTime.UtcNow));
        await context.SaveChangesAsync();
        var rejection = await context.RateSyncRejections.SingleAsync();
        var service = Service(context);

        (await service.AcceptRejectionAsync(rejection.Id.Value, "owner", CancellationToken.None)).Ok.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var rate = await verify.DailyRates.SingleAsync(d => d.Date == date);
        rate.NightlyRate.Amount.Should().Be(240m);
        rate.MinNights.Should().Be(2);
        rate.Source.Should().Be(RateSource.Manual, "accepting makes it an owner-set price");
        (await verify.RateSyncRejections.SingleAsync()).Status.Should().Be(RateSyncRejectionStatus.Accepted);

        // Already settled → both actions refuse.
        (await service.AcceptRejectionAsync(rejection.Id.Value, "owner", CancellationToken.None)).Ok.Should().BeFalse();
        (await service.DismissRejectionAsync(rejection.Id.Value, CancellationToken.None)).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Grid_resolves_each_day_like_the_quote_calculator()
    {
        await using var context = _fixture.CreateContext();
        context.PricingRules.Add(new PricingRule(
            PricingRuleId.New(), new DateOnly(2033, 7, 1), new DateOnly(2033, 7, 31), new Money(200m, "EUR"), 4, 0, 0));
        context.DailyRates.Add(new DailyRate(
            new DateOnly(2033, 7, 10), new Money(250m, "EUR"), 2, RateSource.Manual, DateTime.UtcNow, "owner", isLockedByOwner: true));
        context.DailyRates.Add(new DailyRate(
            new DateOnly(2033, 7, 11), new Money(180m, "EUR"), null, RateSource.PriceLabs, DateTime.UtcNow, "rate-sync"));
        await context.SaveChangesAsync();

        var months = await Read(context).GetGridAsync(new DateOnly(2033, 7, 15), 2, CancellationToken.None);

        months.Should().HaveCount(2);
        months[0].Month.Should().Be(new DateOnly(2033, 7, 1), "the grid starts at the month's first day");
        var days = months[0].Days;
        var overridden = days.Single(d => d.Date == new DateOnly(2033, 7, 10));
        overridden.Should().BeEquivalentTo(new { PriceEur = (decimal?)250m, MinNights = 2, Source = "Manual", HasOverride = true, Locked = true });
        var imported = days.Single(d => d.Date == new DateOnly(2033, 7, 11));
        imported.Source.Should().Be("PriceLabs");
        imported.MinNights.Should().Be(4, "no per-date min falls back to the season's");
        var seasonal = days.Single(d => d.Date == new DateOnly(2033, 7, 20));
        seasonal.Should().BeEquivalentTo(new { PriceEur = (decimal?)200m, MinNights = 4, Source = "Season", HasOverride = false, Locked = false });
        var baseDay = months[1].Days.Single(d => d.Date == new DateOnly(2033, 8, 5));
        baseDay.Should().BeEquivalentTo(new { PriceEur = (decimal?)120m, MinNights = 3, Source = "Base" });
    }

    [Fact]
    public async Task Csv_preview_joins_old_vs_new_and_export_round_trips()
    {
        await using var context = _fixture.CreateContext();
        context.DailyRates.Add(new DailyRate(
            new DateOnly(2034, 3, 1), new Money(100m, "EUR"), 3, RateSource.Manual, DateTime.UtcNow, "owner"));
        await context.SaveChangesAsync();
        var read = Read(context);

        var preview = await read.PreviewCsvAsync("date,price,min_nights\n2034-03-01,130,2\n2034-03-02,110,\nnot-a-date,1,", CancellationToken.None);

        preview.Should().HaveCount(3);
        var changed = preview[0];
        changed.CurrentPriceEur.Should().Be(100m);
        changed.NewPriceEur.Should().Be(130m);
        changed.Changes.Should().BeTrue();
        preview[1].CurrentSource.Should().Be("Base", "no override or season covers that date");
        preview[1].CurrentPriceEur.Should().Be(120m);
        preview[2].Status.Should().StartWith("invalid");

        var csv = await read.ExportRatesCsvAsync(CancellationToken.None);
        csv.Should().StartWith("date,price,min_nights");
        csv.Should().Contain("2034-03-01,100,3");
    }

    private sealed class NoAvailability : GaiaSkyline.Application.Bookings.IAvailabilityService
    {
        public Task<IReadOnlyList<DateOnly>> GetBlockedDatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DateOnly>>([]);

        public void Invalidate()
        {
        }
    }
}
