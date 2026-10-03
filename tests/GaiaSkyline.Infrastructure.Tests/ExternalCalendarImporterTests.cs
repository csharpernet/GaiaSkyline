using System.Net;
using System.Text;
using FluentAssertions;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class ExternalCalendarImporterTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private const string Feed =
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Hostify//EN\r\n" +
        "BEGIN:VEVENT\r\nUID:h1@hostify.com\r\nDTSTART;VALUE=DATE:20290101\r\nDTEND;VALUE=DATE:20290103\r\nSUMMARY:Reserved\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nUID:h2@hostify.com\r\nDTSTART;VALUE=DATE:20290110\r\nDTEND;VALUE=DATE:20290112\r\nSUMMARY:Reserved\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nUID:booking-GS-ECHO@gaiaskyline\r\nDTSTART;VALUE=DATE:20290201\r\nDTEND;VALUE=DATE:20290203\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    [Fact]
    public async Task No_sources_is_manual_mode_and_does_nothing()
    {
        await using var context = _fixture.CreateContext();
        var importer = BuildImporter(context, Feed);

        // Should not throw and should import nothing.
        await importer.ImportAllAsync(CancellationToken.None);

        (await context.ExternalCalendarBlocks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Imports_blocks_upserts_by_uid_and_filters_our_own_echo()
    {
        var protector = new ExternalCalendarUrlProtector(new PassthroughDataProtection());
        var sourceId = ExternalCalendarSourceId.New();
        await using (var seed = _fixture.CreateContext())
        {
            seed.ExternalCalendarSources.Add(new ExternalCalendarSource(
                sourceId, "Hostify", protector.Protect("https://fake.test/feed.ics"), isEnabled: true, DateTime.UtcNow));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var importer = BuildImporter(context, Feed);

        await importer.ImportAllAsync(CancellationToken.None);

        await using var verify = _fixture.CreateContext();
        var blocks = await verify.ExternalCalendarBlocks.Where(b => b.SourceId == sourceId && b.IsActive).ToListAsync();
        blocks.Should().HaveCount(2); // h1 + h2; the gaiaskyline echo is filtered out
        blocks.Select(b => b.ExternalUid).Should().BeEquivalentTo(["h1@hostify.com", "h2@hostify.com"]);
        (await verify.ExternalCalendarSources.FirstAsync(s => s.Id == sourceId)).LastHash.Should().NotBeNullOrEmpty();
    }

    private static ExternalCalendarImporter BuildImporter(Persistence.AppDbContext context, string feed)
    {
        var httpClient = new HttpClient(new StubHandler(feed));
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        return new ExternalCalendarImporter(
            context, httpClient, new ExternalCalendarUrlProtector(new PassthroughDataProtection()),
            availability, new IcsCacheInvalidator(), new NoOpEmailSender(),
            Options.Create(new EmailOptions { OwnerAddress = "owner@test", FromName = "Gaia Skyline" }),
            TimeProvider.System, NullLogger<ExternalCalendarImporter>.Instance);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/calendar"),
            });
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PassthroughDataProtection : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
