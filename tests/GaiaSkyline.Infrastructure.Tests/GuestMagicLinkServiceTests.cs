using FluentAssertions;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class GuestMagicLinkServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Issue_then_consume_works_once_and_rejects_reuse()
    {
        await SeedBookingAsync("GS-ML01", "guest@example.com");
        var clock = new FixedClock(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = _fixture.CreateContext();
        var service = new GuestMagicLinkService(context, new PassthroughDataProtection(), clock);

        var token = await service.IssueAsync("GS-ML01", "guest@example.com", CancellationToken.None);
        token.Should().NotBeNull();

        var first = await service.ConsumeAsync(token!, CancellationToken.None);
        first.Should().Be("GS-ML01");

        // Single use: a second consume of the same token fails.
        var second = await service.ConsumeAsync(token!, CancellationToken.None);
        second.Should().BeNull();
    }

    [Fact]
    public async Task Mismatched_email_returns_null_without_revealing_the_booking()
    {
        await SeedBookingAsync("GS-ML02", "owner@example.com");
        var clock = new FixedClock(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = _fixture.CreateContext();
        var service = new GuestMagicLinkService(context, new PassthroughDataProtection(), clock);

        var token = await service.IssueAsync("GS-ML02", "someone-else@example.com", CancellationToken.None);
        token.Should().BeNull();
    }

    [Fact]
    public async Task Link_expires_after_thirty_minutes()
    {
        await SeedBookingAsync("GS-ML03", "guest@example.com");
        var clock = new FixedClock(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = _fixture.CreateContext();
        var service = new GuestMagicLinkService(context, new PassthroughDataProtection(), clock);

        var token = await service.IssueAsync("GS-ML03", "guest@example.com", CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(31));

        var result = await service.ConsumeAsync(token!, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var clock = new FixedClock(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = _fixture.CreateContext();
        var service = new GuestMagicLinkService(context, new PassthroughDataProtection(), clock);

        var result = await service.ConsumeAsync("not-a-valid-token", CancellationToken.None);
        result.Should().BeNull();
    }

    private async Task SeedBookingAsync(string reference, string email)
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), reference, new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 6),
            2, 0, 0, "Magic Guest", email, "+351 912 345 678", "Portugal", "en",
            nightlyRateSnapshot: new Money(100m, "EUR"), subtotal: new Money(500m, "EUR"),
            discountAmount: new Money(0m, "EUR"), cleaningFee: new Money(60m, "EUR"),
            touristTax: new Money(0m, "EUR"), total: new Money(560m, "EUR"),
            createdAtUtc: DateTime.UtcNow);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    // Identity-free data protection for tests: the string extensions base64url round-trip these bytes.
    private sealed class PassthroughDataProtection : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
