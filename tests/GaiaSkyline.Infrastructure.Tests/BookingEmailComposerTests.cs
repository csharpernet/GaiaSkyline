using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Data;
using GaiaSkyline.Infrastructure.Notifications;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class BookingEmailComposerTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>, IDisposable
{
    private readonly LocalDbFixture _fixture = fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Guest_confirmation_goes_to_the_guest_with_reference_and_total()
    {
        var booking = Build("GS-TEST1", "ana@example.com", "Ana", "en");

        var message = await Composer().ComposeAsync(booking, BookingEmailKind.Confirmation, CancellationToken.None);

        message.ToAddress.Should().Be("ana@example.com");
        message.Subject.Should().Contain("GS-TEST1");
        message.HtmlBody.Should().Contain("Ana");
        message.HtmlBody.Should().Contain("GS-TEST1");
        message.HtmlBody.Should().Contain("€360.00");
        // Confirmation link carries the signed token and the language slug.
        message.HtmlBody.Should().Contain("/en/book/confirmation/GS-TEST1");
        message.HtmlBody.Should().Contain("token=TOKEN-GS-TEST1");
    }

    [Fact]
    public async Task Owner_notification_goes_to_the_owner_in_english()
    {
        var booking = Build("GS-TEST2", "guest@example.com", "Guest", "pt-PT");

        var message = await Composer().ComposeAsync(booking, BookingEmailKind.OwnerNotification, CancellationToken.None);

        message.ToAddress.Should().Be("owner@gaiaskyline.test");
        message.HtmlBody.Should().Contain("New confirmed booking");
        message.HtmlBody.Should().Contain("GS-TEST2");
    }

    [Fact]
    public async Task Multibanco_email_includes_the_entity_and_reference()
    {
        var booking = Build("GS-TEST3", "mb@example.com", "Mba", "en");
        booking.SetMultibancoVoucher("12345", "999888777", DateTime.UtcNow.AddDays(5));

        var message = await Composer().ComposeAsync(booking, BookingEmailKind.MultibancoReference, CancellationToken.None);

        message.ToAddress.Should().Be("mb@example.com");
        message.HtmlBody.Should().Contain("12345");
        message.HtmlBody.Should().Contain("999888777");
    }

    private BookingEmailComposer Composer()
    {
        var content = new ContentService(new ContentReadStore(_fixture.CreateContext()), _cache, new ContentRevision());
        var options = Options.Create(new EmailOptions
        {
            OwnerAddress = "owner@gaiaskyline.test",
            FromName = "Gaia Skyline",
            SiteBaseUrl = "https://book.test",
        });
        return new BookingEmailComposer(content, new FakeTokenService(), options);
    }

    private static Booking Build(string reference, string email, string name, string language) =>
        new(
            BookingId.New(), reference,
            new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 6),
            2, 0, 0, name, email, "+351 912 345 678", "Portugal", language,
            nightlyRateSnapshot: new Money(100m, "EUR"),
            subtotal: new Money(500m, "EUR"),
            discountAmount: new Money(200m, "EUR"),
            cleaningFee: new Money(60m, "EUR"),
            touristTax: new Money(0m, "EUR"),
            total: new Money(360m, "EUR"),
            createdAtUtc: DateTime.UtcNow);

    private sealed class FakeTokenService : IBookingTokenService
    {
        public string CreateConfirmationToken(string referenceCode) => "TOKEN-" + referenceCode;

        public bool IsValidConfirmationToken(string referenceCode, string token) => token == "TOKEN-" + referenceCode;
    }
}
