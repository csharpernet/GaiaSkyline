using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>The admin booking read + action services: filters, status transitions, notes and sync. Stage 7 §6.</summary>
public sealed class AdminBookingServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task List_filters_by_status_and_search()
    {
        var email = $"filter-{Guid.NewGuid():N}@example.com";
        var confirmed = Booking("CONFIRMED", email);
        confirmed.ConfirmPayment("card", Now);
        var awaiting = Booking("AWAITING", email);
        await SeedAsync(confirmed, awaiting);

        await using var ctx = _fixture.CreateContext();
        var read = new AdminBookingReadService(ctx);

        // Search alone → both of mine.
        var all = await read.GetAsync(new BookingAdminFilter(null, email, null, null), CancellationToken.None);
        all.Should().HaveCount(2);

        // Search + status → only the confirmed one.
        var onlyConfirmed = await read.GetAsync(
            new BookingAdminFilter(BookingStatus.Confirmed, email, null, null), CancellationToken.None);
        onlyConfirmed.Should().ContainSingle().Which.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Detail_exposes_the_legal_next_statuses()
    {
        var booking = Booking($"REF{Guid.NewGuid():N}"[..12], "d@example.com");
        booking.ConfirmPayment("card", Now);
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var read = new AdminBookingReadService(ctx);

        var detail = await read.GetDetailAsync(booking.Id.Value, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Status.Should().Be(BookingStatus.Confirmed);
        detail.AllowedTransitions.Should().Contain(BookingStatus.CheckedIn)
            .And.Contain(BookingStatus.Cancelled)
            .And.NotContain(BookingStatus.Completed);
    }

    [Fact]
    public async Task CheckIn_advances_a_confirmed_booking_but_rejects_an_awaiting_one()
    {
        var confirmed = Booking($"CI{Guid.NewGuid():N}"[..12], "c@example.com");
        confirmed.ConfirmPayment("card", Now);
        var awaiting = Booking($"AW{Guid.NewGuid():N}"[..12], "a@example.com");
        await SeedAsync(confirmed, awaiting);

        await using var ctx = _fixture.CreateContext();
        var svc = new AdminBookingService(ctx, new FakeLifecycle(), TimeProvider.System);

        (await svc.CheckInAsync(confirmed.Id.Value, CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.CheckInAsync(awaiting.Id.Value, CancellationToken.None)).Ok.Should().BeFalse("AwaitingPayment can't check in");
        (await svc.CheckInAsync(Guid.NewGuid(), CancellationToken.None)).Ok.Should().BeFalse("unknown id");

        await using var verify = _fixture.CreateContext();
        var reread = new AdminBookingReadService(verify);
        (await reread.GetDetailAsync(confirmed.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.CheckedIn);
    }

    [Fact]
    public async Task Refund_marks_full_and_partial_states()
    {
        var full = Booking($"RF{Guid.NewGuid():N}"[..12], "f@example.com");
        full.ConfirmPayment("card", Now);
        var partial = Booking($"RP{Guid.NewGuid():N}"[..12], "p@example.com");
        partial.ConfirmPayment("card", Now);
        await SeedAsync(full, partial);

        await using var ctx = _fixture.CreateContext();
        var svc = new AdminBookingService(ctx, new FakeLifecycle(), TimeProvider.System);

        (await svc.RefundAsync(full.Id.Value, partial: false, CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.RefundAsync(partial.Id.Value, partial: true, CancellationToken.None)).Ok.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var reread = new AdminBookingReadService(verify);
        (await reread.GetDetailAsync(full.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.Refunded);
        (await reread.GetDetailAsync(partial.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.PartiallyRefunded);
    }

    [Fact]
    public async Task Notes_and_sync_persist()
    {
        var booking = Booking($"NS{Guid.NewGuid():N}"[..12], "n@example.com");
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var svc = new AdminBookingService(ctx, new FakeLifecycle(), TimeProvider.System);

        (await svc.SetNotesAsync(booking.Id.Value, "Late arrival ~23:00", CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.MarkSyncedAsync(booking.Id.Value, "Mirrored in Hostify", CancellationToken.None)).Ok.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var reread = new AdminBookingReadService(verify);
        var detail = await reread.GetDetailAsync(booking.Id.Value, CancellationToken.None);
        detail!.Notes.Should().Be("Late arrival ~23:00");
        detail.ExternalChannelSyncedAtUtc.Should().NotBeNull();
        detail.ExternalChannelSyncNote.Should().Be("Mirrored in Hostify");
    }

    [Fact]
    public async Task Cancel_delegates_to_the_lifecycle_service_and_guards_unknown_ids()
    {
        var booking = Booking($"CX{Guid.NewGuid():N}"[..12], "x@example.com");
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var lifecycle = new FakeLifecycle();
        var svc = new AdminBookingService(ctx, lifecycle, TimeProvider.System);

        (await svc.CancelAsync(booking.Id.Value, "guest asked", CancellationToken.None)).Ok.Should().BeTrue();
        lifecycle.Cancelled.Should().ContainSingle().Which.Should().Be(booking.Id.Value);

        (await svc.CancelAsync(Guid.NewGuid(), null, CancellationToken.None)).Ok.Should().BeFalse("unknown id");
    }

    private static Booking Booking(string reference, string email) => new(
        BookingId.New(), reference, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4),
        adults: 2, children: 0, infants: 0,
        guestName: "Guest", guestEmail: email, guestPhone: "+351000000000", guestCountry: "PT", guestLanguage: "en",
        nightlyRateSnapshot: new Money(100m, "EUR"), subtotal: new Money(300m, "EUR"), discountAmount: new Money(0m, "EUR"),
        cleaningFee: new Money(0m, "EUR"), touristTax: new Money(0m, "EUR"), total: new Money(300m, "EUR"),
        createdAtUtc: Now);

    private async Task SeedAsync(params Booking[] bookings)
    {
        await using var ctx = _fixture.CreateContext();
        ctx.Bookings.AddRange(bookings);
        await ctx.SaveChangesAsync();
    }

    private sealed class FakeLifecycle : IBookingLifecycleService
    {
        public List<Guid> Cancelled { get; } = [];

        public Task CancelAndReleaseAsync(BookingId bookingId, string? reason, CancellationToken cancellationToken)
        {
            Cancelled.Add(bookingId.Value);
            return Task.CompletedTask;
        }
    }
}
