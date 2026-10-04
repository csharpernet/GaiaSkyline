using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
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
    }

    private static async Task EnsureBookingAsync(IServiceProvider services, E2EOptions options, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var magicRef = options.BookingReference.Trim().ToUpperInvariant();
        var confirmedRef = $"{magicRef}-C";

        // Reset both on every startup so the destructive tests (magic-link cancel; dashboard "Done in
        // Hostify") always find fresh fixtures — a re-run against the same database must still pass.
        await ResetBookingAsync(dbContext, magicRef, cancellationToken);
        await ResetBookingAsync(dbContext, confirmedRef, cancellationToken);

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
