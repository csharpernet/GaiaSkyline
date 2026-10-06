using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Notifications;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class BookingEmailDispatcherTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>, IDisposable
{
    private readonly LocalDbFixture _fixture = fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Property_manager_email_is_skipped_when_none_configured()
    {
        var id = await SeedBookingAsync("GS-PMD1");
        var sender = new RecordingSender();
        var dispatcher = BuildDispatcher(sender, []);

        await dispatcher.DispatchAsync(id, BookingEmailKind.PropertyManager, CancellationToken.None);

        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Property_manager_email_is_sent_to_each_configured_address()
    {
        var id = await SeedBookingAsync("GS-PMD2");
        var sender = new RecordingSender();
        var dispatcher = BuildDispatcher(sender, ["ops@hostify.test", "manager@hostify.test"]);

        await dispatcher.DispatchAsync(id, BookingEmailKind.PropertyManager, CancellationToken.None);

        sender.Sent.Should().HaveCount(2);
        sender.Sent.Select(m => m.ToAddress).Should().BeEquivalentTo(["ops@hostify.test", "manager@hostify.test"]);
        sender.Sent.Should().OnlyContain(m => m.HtmlBody.Contains("Please block these dates in Hostify"));
    }

    private BookingEmailDispatcher BuildDispatcher(IEmailSender sender, string[] propertyManagerEmails)
    {
        var composer = new BookingEmailComposer(
            new ContentService(new ContentReadStore(_fixture.CreateContext()), _cache, new ContentRevision()),
            new StubTokenService(),
            new FakeGuestDocumentService(),
            TestOptions.Snapshot(new EmailOptions { OwnerAddress = "owner@test", FromName = "Gaia Skyline" }));
        var pmOptions = TestOptions.Snapshot(new PropertyManagerOptions { NotificationEmails = propertyManagerEmails });
        return new BookingEmailDispatcher(_fixture.CreateContext(), composer, sender, pmOptions, NullLogger<BookingEmailDispatcher>.Instance);
    }

    private async Task<Guid> SeedBookingAsync(string reference)
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), reference, new DateOnly(2027, 8, 1), new DateOnly(2027, 8, 5),
            2, 0, 0, "Guest", "guest@example.com", "+351 912 345 678", "Portugal", "en",
            new Money(100m, "EUR"), new Money(400m, "EUR"), new Money(0m, "EUR"),
            new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(460m, "EUR"), DateTime.UtcNow);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return booking.Id.Value;
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTokenService : IBookingTokenService
    {
        public string CreateConfirmationToken(string referenceCode) => "t-" + referenceCode;

        public bool IsValidConfirmationToken(string referenceCode, string token) => true;
    }
}
