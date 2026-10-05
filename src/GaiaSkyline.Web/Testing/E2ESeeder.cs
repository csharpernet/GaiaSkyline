using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Testing;

/// <summary>
/// Seeds deterministic fixtures for the Playwright E2E suite (E2E only): the Owner gets a known
/// authenticator key with 2FA enabled (so a test can compute a valid TOTP), and a known
/// AwaitingPayment booking exists for the guest magic-link → cancel flow. Idempotent.
/// </summary>
public static class E2ESeeder
{
    // ASP.NET Core Identity stores the authenticator key as a user token under this provider + name.
    private const string AuthenticatorKeyProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyName = "AuthenticatorKey";
    private const string Currency = "EUR";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<E2EOptions>>().Value;
        await EnsureOwnerTwoFactorAsync(services, options, cancellationToken);
        await EnsureBookingAsync(services, options, cancellationToken);
        await EnsureCalendarFixturesAsync(services, options, cancellationToken);
    }

    /// <summary>
    /// Seeds a live hero video (E2E only) with real DB rows but placeholder /media URLs. The Playwright hero
    /// tests assert JS behaviour (poster-only on reduced-motion/Save-Data, portrait source selection), which
    /// never fetches the video bytes — so no FFmpeg or real files are needed. Called on demand (not at startup)
    /// so the Lighthouse run, which happens first, still measures the poster-only fallback. Idempotent.
    /// </summary>
    public static async Task SeedHeroAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        var existing = await dbContext.HeroVideos.ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            dbContext.HeroVideos.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var id = HeroVideoId.New();
        var stem = $"hero-e2e-{id.Value:N}";
        var hero = HeroVideo.CreatePending(
            id, "e2e-sample.mp4", 1_000_000, sourceDurationSec: 20,
            trimStartSec: 0, trimEndSec: 15, crossfadeSec: 1, focalX: 0.5, now, "e2e");
        hero.MarkTranscoding();
        hero.SetRenditions(Enum.GetValues<HeroRenditionKind>().Select(kind =>
        {
            var ext = kind is HeroRenditionKind.DesktopH264 or HeroRenditionKind.MobileH264 ? "mp4" : "webm";
            var orientation = kind.IsMobile() ? "mobile" : "desktop";
            var codec = kind.ToString().Contains("Av1", StringComparison.Ordinal) ? "av1"
                : kind.ToString().Contains("Vp9", StringComparison.Ordinal) ? "vp9" : "h264";
            var (w, h) = kind.IsMobile() ? (720, 1280) : (1920, 1080);
            return new HeroVideoRendition(
                HeroVideoRenditionId.New(), id, kind, $"/media/{stem}-{orientation}-{codec}.{ext}", w, h, 1_500_000);
        }));
        hero.SetPosters($"/media/{stem}-poster-desktop-1600.jpg", null, $"/media/{stem}-poster-mobile-1600.jpg", null);
        hero.MarkReady(now);
        hero.Promote();
        dbContext.HeroVideos.Add(hero);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate the home output cache so the seeded hero renders on the next request.
        services.GetService<GaiaSkyline.Application.Content.IContentRevision>()?.Bump();
    }

    private static async Task EnsureOwnerTwoFactorAsync(IServiceProvider services, E2EOptions options, CancellationToken cancellationToken)
    {
        var ownerEmail = services.GetRequiredService<IConfiguration>()["Owner:Email"];
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var owner = await userManager.FindByEmailAsync(ownerEmail);
        if (owner is null)
        {
            return;
        }

        // Pin a deterministic authenticator key and enable 2FA so the test can compute a valid TOTP.
        await userManager.SetAuthenticationTokenAsync(owner, AuthenticatorKeyProvider, AuthenticatorKeyName, options.OwnerTotpKey);
        if (!await userManager.GetTwoFactorEnabledAsync(owner))
        {
            await userManager.SetTwoFactorEnabledAsync(owner, true);
        }

        // Disable Identity lockout for the shared E2E owner and clear any carried-over counter. The suite
        // signs this one account in many times in a row, and an occasional TOTP that rolls over at a 30s
        // boundary counts as a failed access attempt — enough logins and the account would lock out mid-run,
        // redirecting the password step back to /admin/login. Lockout stays fully enabled in production
        // (this seam throws if ever enabled there — see Program.cs).
        await userManager.SetLockoutEnabledAsync(owner, false);
        await userManager.SetLockoutEndDateAsync(owner, null);
        await userManager.ResetAccessFailedCountAsync(owner);
    }

    private static async Task EnsureBookingAsync(IServiceProvider services, E2EOptions options, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var magicRef = options.BookingReference.Trim().ToUpperInvariant();
        var confirmedRef = $"{magicRef}-C";
        var adminRef = $"{magicRef}-B";
        var multibancoRef = $"{magicRef}-M";

        // Reset all fixtures on every startup so the destructive tests (magic-link cancel; dashboard "Done in
        // Hostify"; bookings-admin check-in; payments Multibanco cancel) always find fresh fixtures.
        await ResetBookingAsync(dbContext, magicRef, cancellationToken);
        await ResetBookingAsync(dbContext, confirmedRef, cancellationToken);
        await ResetBookingAsync(dbContext, adminRef, cancellationToken);
        await ResetBookingAsync(dbContext, multibancoRef, cancellationToken);

        // AwaitingPayment booking for the guest magic-link → cancel flow (cancellable; no Stripe refund).
        var magicCheckIn = DateOnly.FromDateTime(now).AddDays(60);
        var magic = BuildBooking(magicRef, options.GuestEmail, magicCheckIn, now);
        dbContext.Bookings.Add(magic);
        AddOccupancy(dbContext, magic);

        // Confirmed booking (confirmed 48 h ago, so it shows overdue) for the dashboard manual-sync to-do.
        var confirmedCheckIn = DateOnly.FromDateTime(now).AddDays(90);
        var confirmed = BuildBooking(confirmedRef, options.GuestEmail, confirmedCheckIn, now);
        confirmed.ConfirmPayment("card", now.AddHours(-48));
        dbContext.Bookings.Add(confirmed);
        AddOccupancy(dbContext, confirmed);

        // A second Confirmed booking for the bookings-admin test to mutate (check-in). It is marked synced so
        // it never lands on the dashboard manual-sync to-do, keeping that test's single-item expectation intact.
        var adminCheckIn = DateOnly.FromDateTime(now).AddDays(75);
        var adminBooking = BuildBooking(adminRef, options.GuestEmail, adminCheckIn, now);
        adminBooking.ConfirmPayment("card", now.AddHours(-48));
        adminBooking.MarkExternalChannelSynced(now, "seed");
        dbContext.Bookings.Add(adminBooking);
        AddOccupancy(dbContext, adminBooking);

        // A Multibanco hold (voucher far from expiry so the booking-expiry job never races the test),
        // dedicated to the payments-admin monitor + typed-cancel flow. Never reuse the magic-link booking.
        var multibancoCheckIn = DateOnly.FromDateTime(now).AddDays(45);
        var multibanco = BuildBooking(multibancoRef, options.GuestEmail, multibancoCheckIn, now);
        multibanco.AttachPaymentIntent($"pi_e2e_{Guid.NewGuid():N}");
        multibanco.SetMultibancoVoucher("12345", "123456789", now.AddDays(30));
        dbContext.Bookings.Add(multibanco);
        AddOccupancy(dbContext, multibanco);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deterministic calendar fixtures (Stage 7 §7): one block of each kind far in the future, plus an
    /// open conflict against the Confirmed fixture booking for the resolve flow. Reset on every startup
    /// so destructive calendar tests always find them fresh.
    /// </summary>
    private static async Task EnsureCalendarFixturesAsync(
        IServiceProvider services, E2EOptions options, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        const string Marker = "E2E seed";
        const string ConflictSource = "E2E-Source";

        var staleBlocks = await dbContext.OwnerBlocks
            .Where(b => b.Note != null && b.Note.StartsWith(Marker)).ToListAsync(cancellationToken);
        dbContext.OwnerBlocks.RemoveRange(staleBlocks);
        var staleConflicts = await dbContext.BookingConflicts
            .Where(c => c.SourceName == ConflictSource).ToListAsync(cancellationToken);
        dbContext.BookingConflicts.RemoveRange(staleConflicts);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.OwnerBlocks.Add(new Domain.Availability.OwnerBlock(
            Domain.Identifiers.OwnerBlockId.New(), today.AddDays(120), today.AddDays(123),
            Domain.Availability.OwnerBlockKind.OwnerUnavailable, $"{Marker} — owner hold", now, "e2e"));
        dbContext.OwnerBlocks.Add(new Domain.Availability.OwnerBlock(
            Domain.Identifiers.OwnerBlockId.New(), today.AddDays(130), today.AddDays(133),
            Domain.Availability.OwnerBlockKind.ExternalBooking, $"{Marker} — Hostify copy", now, "e2e"));

        // The confirmed fixture booking sits at +90d; a fake imported range colliding with it.
        var confirmedRef = $"{options.BookingReference.Trim().ToUpperInvariant()}-C";
        dbContext.BookingConflicts.Add(new Domain.Availability.BookingConflict(
            Domain.Identifiers.BookingConflictId.New(), confirmedRef, ConflictSource,
            today.AddDays(90), today.AddDays(93), now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deterministic calendar fixtures (Stage 7 §7): one block of each kind far in the future, plus an
    /// open conflict against the Confirmed fixture booking for the resolve flow. Reset on every startup
    /// so destructive calendar tests always find them fresh.
    /// </summary>
    private static async Task EnsureCalendarFixturesAsync(
        IServiceProvider services, E2EOptions options, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        const string Marker = "E2E seed";
        const string ConflictSource = "E2E-Source";

        var staleBlocks = await dbContext.OwnerBlocks
            .Where(b => b.Note != null && b.Note.StartsWith(Marker)).ToListAsync(cancellationToken);
        dbContext.OwnerBlocks.RemoveRange(staleBlocks);
        var staleConflicts = await dbContext.BookingConflicts
            .Where(c => c.SourceName == ConflictSource).ToListAsync(cancellationToken);
        dbContext.BookingConflicts.RemoveRange(staleConflicts);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.OwnerBlocks.Add(new Domain.Availability.OwnerBlock(
            Domain.Identifiers.OwnerBlockId.New(), today.AddDays(120), today.AddDays(123),
            Domain.Availability.OwnerBlockKind.OwnerUnavailable, $"{Marker} — owner hold", now, "e2e"));
        dbContext.OwnerBlocks.Add(new Domain.Availability.OwnerBlock(
            Domain.Identifiers.OwnerBlockId.New(), today.AddDays(130), today.AddDays(133),
            Domain.Availability.OwnerBlockKind.ExternalBooking, $"{Marker} — Hostify copy", now, "e2e"));

        // The confirmed fixture booking sits at +90d; a fake imported range colliding with it.
        var confirmedRef = $"{options.BookingReference.Trim().ToUpperInvariant()}-C";
        dbContext.BookingConflicts.Add(new Domain.Availability.BookingConflict(
            Domain.Identifiers.BookingConflictId.New(), confirmedRef, ConflictSource,
            today.AddDays(90), today.AddDays(93), now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task ResetBookingAsync(AppDbContext dbContext, string reference, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Bookings.FirstOrDefaultAsync(b => b.ReferenceCode == reference, cancellationToken);
        if (existing is null)
        {
            return;
        }

        var staleOccupancy = await dbContext.BookingDateOccupancies
            .Where(o => o.BookingId == existing.Id).ToListAsync(cancellationToken);
        dbContext.BookingDateOccupancies.RemoveRange(staleOccupancy);
        dbContext.Bookings.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Booking BuildBooking(string reference, string guestEmail, DateOnly checkIn, DateTime now) =>
        new(BookingId.New(), reference, checkIn, checkIn.AddDays(3),
            adults: 2, children: 0, infants: 0,
            guestName: "E2E Guest", guestEmail: guestEmail, guestPhone: "+351000000000",
            guestCountry: "PT", guestLanguage: "en",
            nightlyRateSnapshot: new Money(100m, Currency),
            subtotal: new Money(300m, Currency), discountAmount: new Money(0m, Currency),
            cleaningFee: new Money(0m, Currency), touristTax: new Money(0m, Currency), total: new Money(300m, Currency),
            createdAtUtc: now);

    private static void AddOccupancy(AppDbContext dbContext, Booking booking)
    {
        for (var date = booking.CheckIn; date < booking.CheckOut; date = date.AddDays(1))
        {
            dbContext.BookingDateOccupancies.Add(new BookingDateOccupancy(date, booking.Id));
        }
    }
}
