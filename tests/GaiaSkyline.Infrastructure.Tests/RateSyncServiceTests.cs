using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class RateSyncServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    private static readonly DateOnly Soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    public RateSyncServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        // The class fixture shares one database across the class; start each test from a clean table.
        using var context = _fixture.CreateContext();
        context.DailyRates.ExecuteDelete();
        context.RateSyncRejections.ExecuteDelete();
    }

    private static Money Eur(decimal amount) => new(amount, "EUR");

    [Fact]
    public async Task No_provider_configured_is_manual_mode_and_imports_nothing()
    {
        await using var context = _fixture.CreateContext();
        var service = BuildService(context, providers: [], options: new PricingProviderOptions());

        await service.SyncAsync(CancellationToken.None);

        (await context.DailyRates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Imports_provider_rates_as_daily_rates()
    {
        var provider = new FakeRateProvider([new ProviderRate(Soon, 120m, 2)]);
        var options = new PricingProviderOptions { Provider = RateProviderType.Hostify };

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [provider], options).SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var rate = await verify.DailyRates.SingleAsync(d => d.Date == Soon);
        rate.NightlyRate.Amount.Should().Be(120m);
        rate.MinNights.Should().Be(2);
        rate.Source.Should().Be(RateSource.Hostify);
    }

    [Fact]
    public async Task Skips_owner_locked_dates()
    {
        await using (var seed = _fixture.CreateContext())
        {
            var locked = new DailyRate(Soon, Eur(150m), null, RateSource.Manual, DateTime.UtcNow, "owner", isLockedByOwner: true);
            seed.DailyRates.Add(locked);
            await seed.SaveChangesAsync();
        }

        var provider = new FakeRateProvider([new ProviderRate(Soon, 999m, null)]);
        var options = new PricingProviderOptions { Provider = RateProviderType.Hostify };

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [provider], options).SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var rate = await verify.DailyRates.SingleAsync(d => d.Date == Soon);
        rate.NightlyRate.Amount.Should().Be(150m); // unchanged
        rate.Source.Should().Be(RateSource.Manual);
        rate.IsLockedByOwner.Should().BeTrue();
    }

    [Fact]
    public async Task Applies_the_direct_booking_adjustment_rounded_to_whole_euros()
    {
        // 95 * 0.9 = 85.5 -> 86 (away from zero).
        var provider = new FakeRateProvider([new ProviderRate(Soon, 95m, null)]);
        var options = new PricingProviderOptions { Provider = RateProviderType.PriceLabs, DirectBookingAdjustmentPct = -10m };

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [provider], options).SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        (await verify.DailyRates.SingleAsync(d => d.Date == Soon)).NightlyRate.Amount.Should().Be(86m);
    }

    [Fact]
    public async Task Rejects_prices_outside_the_bounds_keeping_the_previous_value_and_alerts_the_owner()
    {
        await using (var seed = _fixture.CreateContext())
        {
            seed.DailyRates.Add(new DailyRate(Soon, Eur(150m), null, RateSource.Manual, DateTime.UtcNow, "owner"));
            await seed.SaveChangesAsync();
        }

        var provider = new FakeRateProvider([new ProviderRate(Soon, 300m, null)]);
        var options = new PricingProviderOptions { Provider = RateProviderType.Hostify, CeilingPrice = 200m };
        var email = new RecordingEmailSender();

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [provider], options, email).SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        (await verify.DailyRates.SingleAsync(d => d.Date == Soon)).NightlyRate.Amount.Should().Be(150m); // kept
        email.Sent.Should().ContainSingle();

        // The rejection is persisted open for the §8 review page.
        var rejection = await verify.RateSyncRejections.SingleAsync(r => r.Date == Soon);
        rejection.Status.Should().Be(RateSyncRejectionStatus.Open);
        rejection.OfferedPriceEur.Should().Be(300m);
        rejection.Reason.Should().Contain("ceiling");
    }

    [Fact]
    public async Task A_repeat_rejection_for_the_same_date_updates_the_open_row_instead_of_piling_up()
    {
        var options = new PricingProviderOptions { Provider = RateProviderType.Hostify, CeilingPrice = 200m };

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [new FakeRateProvider([new ProviderRate(Soon, 300m, null)])], options)
                .SyncAsync(CancellationToken.None);
        }

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [new FakeRateProvider([new ProviderRate(Soon, 350m, 2)])], options)
                .SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var rejection = await verify.RateSyncRejections.SingleAsync(r => r.Date == Soon);
        rejection.OfferedPriceEur.Should().Be(350m, "the later offer replaces the open one");
        rejection.OfferedMinNights.Should().Be(2);
    }

    [Fact]
    public async Task Upserts_only_changed_dates()
    {
        await using (var seed = _fixture.CreateContext())
        {
            // Already matches what the provider will return (same price + min nights).
            seed.DailyRates.Add(new DailyRate(Soon, Eur(120m), 2, RateSource.Hostify, DateTime.UtcNow, "seed"));
            await seed.SaveChangesAsync();
        }

        var provider = new FakeRateProvider([new ProviderRate(Soon, 120m, 2)]);
        var options = new PricingProviderOptions { Provider = RateProviderType.Hostify };

        await using (var context = _fixture.CreateContext())
        {
            await BuildService(context, [provider], options).SyncAsync(CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        // Untouched: the identical row keeps its original UpdatedBy rather than being rewritten by "rate-sync".
        (await verify.DailyRates.SingleAsync(d => d.Date == Soon)).UpdatedBy.Should().Be("seed");
    }

    private static RateSyncService BuildService(
        AppDbContext context,
        IReadOnlyList<IRateProvider> providers,
        PricingProviderOptions options,
        IEmailSender? email = null)
    {
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        return new RateSyncService(
            context, providers, Options.Create(options), new ContentRevision(), availability,
            email ?? new RecordingEmailSender(),
            Options.Create(new EmailOptions { OwnerAddress = "owner@test", FromName = "Gaia Skyline" }),
            TimeProvider.System, NullLogger<RateSyncService>.Instance);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
