using System.Net;
using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>The §12 settings store (secrets round-trip, masking, overrides) and the settings admin service.</summary>
public sealed class SettingsAdminTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;
    private readonly ServiceProvider _provider;

    public SettingsAdminTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using (var context = _fixture.CreateContext())
        {
            context.SiteSettings.ExecuteDelete();
            context.ExternalCalendarSources.ExecuteDelete();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(_fixture.ConnectionString));
        _provider = services.BuildServiceProvider();
    }

    // Not passthrough: the "stored value is ciphertext" assertions need Protect(x) != x.
    private static readonly IDataProtectionProvider DataProtection = new ObfuscatingDataProtection();

    private static SiteSettingsProtector Protector() => new(DataProtection);

    private SiteSettingsSnapshot Snapshot() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(), Protector(), NullLogger<SiteSettingsSnapshot>.Instance);

    [Fact]
    public async Task Secrets_round_trip_encrypted_and_surface_masked()
    {
        await using var context = _fixture.CreateContext();
        var snapshot = Snapshot();
        var writer = new SiteSettingsWriter(context, snapshot, Protector(), TimeProvider.System);

        await writer.SetAsync(SettingKeys.PricingApiKey, "pl_secret_9876", isSecret: true, CancellationToken.None);

        (await context.SiteSettings.SingleAsync()).Value.Should().NotContain("pl_secret",
            "the stored value must be ciphertext");
        snapshot.Get(SettingKeys.PricingApiKey).Should().Be("pl_secret_9876");
        snapshot.GetMasked(SettingKeys.PricingApiKey).Should().Be("••••9876");

        // Blank deletes the override.
        await writer.SetAsync(SettingKeys.PricingApiKey, null, isSecret: true, CancellationToken.None);
        snapshot.Get(SettingKeys.PricingApiKey).Should().BeNull();
        (await context.SiteSettings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Overrides_apply_onto_configured_defaults_only_when_set()
    {
        var settings = new FakeSettings(new()
        {
            [SettingKeys.PricingProvider] = "PriceLabs",
            [SettingKeys.PricingFloor] = "55.5",
            [SettingKeys.PropertyManagerEmails] = "a@x.com; b@x.com",
            [SettingKeys.OwnerEmail] = "owner@override.test",
        });

        var pricing = new PricingProviderOptions { CeilingPrice = 900m };
        SettingsOverrides.Apply(pricing, settings);
        pricing.Provider.Should().Be(RateProviderType.PriceLabs);
        pricing.FloorPrice.Should().Be(55.5m);
        pricing.CeilingPrice.Should().Be(900m, "no override row leaves the configured value");

        var pm = new PropertyManagerOptions();
        SettingsOverrides.Apply(pm, settings);
        pm.NotificationEmails.Should().BeEquivalentTo("a@x.com", "b@x.com");

        var email = new EmailOptions { OwnerAddress = "configured@x.com" };
        SettingsOverrides.Apply(email, settings);
        email.OwnerAddress.Should().Be("owner@override.test");
    }

    [Fact]
    public async Task Calendar_sources_store_encrypted_urls_and_surface_them_masked()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context, out _);

        (await service.AddCalendarSourceAsync("Hostify", "https://feeds.example.com/cal-7731.ics", enabled: false, CancellationToken.None))
            .Ok.Should().BeTrue();
        (await service.AddCalendarSourceAsync("Hostify", "https://x.example.com/x.ics", enabled: false, CancellationToken.None))
            .Ok.Should().BeFalse("names are unique");
        (await service.AddCalendarSourceAsync("Bad", "not-a-url", enabled: false, CancellationToken.None))
            .Ok.Should().BeFalse();

        var sources = await service.GetCalendarSourcesAsync(CancellationToken.None);
        var source = sources.Should().ContainSingle().Subject;
        source.MaskedUrl.Should().EndWith(".ics").And.StartWith("••••");
        source.MaskedUrl.Should().NotContain("example.com", "only the last characters may show");
        (await context.ExternalCalendarSources.SingleAsync()).IcsUrlProtected
            .Should().NotContain("example.com", "the URL is stored encrypted");

        (await service.SetCalendarEnabledAsync(source.Id, true, CancellationToken.None)).Ok.Should().BeTrue();
        (await service.GetCalendarSourcesAsync(CancellationToken.None)).Single().IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Test_fetch_parses_without_saving_and_property_update_bumps_the_revision()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context, out var revision);

        var fetch = await service.TestFetchAsync(null, "https://fixture.test/feed.ics", CancellationToken.None);
        fetch.Ok.Should().BeTrue(fetch.Error);
        fetch.Events.Should().Be(2);
        fetch.From.Should().Be(new DateOnly(2030, 1, 10));
        (await context.ExternalCalendarBlocks.CountAsync()).Should().Be(0, "test fetch never persists");

        // Property update (seed one first) bumps the revision so public pages refresh.
        context.Properties.Add(new GaiaSkyline.Domain.Entities.Property(
            GaiaSkyline.Domain.Identifiers.PropertyId.New(), "Gaia Skyline", "AL/12345", "Some street 1, Gaia",
            41.13, -8.61, "EUR", "Europe/Lisbon", new TimeOnly(16, 0), new TimeOnly(11, 0), 6, 2, 3, 1, "2+1 beds"));
        await context.SaveChangesAsync();
        var before = revision.Current;
        var property = await service.GetPropertyAsync(CancellationToken.None);
        (await service.UpdatePropertyAsync(property! with { Sleeps = 5 }, CancellationToken.None)).Ok.Should().BeTrue();
        revision.Current.Should().BeGreaterThan(before);
        (await context.Properties.SingleAsync()).Sleeps.Should().Be(5);
    }

    private static SettingsAdminService Service(AppDbContext context, out ContentRevision revision)
    {
        revision = new ContentRevision();
        return new SettingsAdminService(
            context,
            new ExternalCalendarUrlProtector(DataProtection),
            new FixtureHttpClientFactory(),
            revision,
            TimeProvider.System);
    }

    private sealed class FakeSettings(Dictionary<string, string> values) : ISiteSettings
    {
        public string? Get(string key) => values.GetValueOrDefault(key);

        public string? GetMasked(string key) => Get(key);

        public void Reload()
        {
        }
    }

    private sealed class ObfuscatingDataProtection : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => [0x5A, .. plaintext.Reverse()];

        public byte[] Unprotect(byte[] protectedData) => protectedData.Skip(1).Reverse().ToArray();
    }

    private sealed class FixtureHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FixtureHandler());
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private const string Ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:a@t\r\nDTSTART;VALUE=DATE:20300110\r\nDTEND;VALUE=DATE:20300113\r\nSUMMARY:One\r\nEND:VEVENT\r\nBEGIN:VEVENT\r\nUID:b@t\r\nDTSTART;VALUE=DATE:20300120\r\nDTEND;VALUE=DATE:20300122\r\nSUMMARY:Two\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Ics) });
    }
}
