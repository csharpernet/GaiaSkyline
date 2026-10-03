using System.Globalization;
using System.Text;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The confirmation page. The reference is human-readable, so a valid signed token is also required
/// (links can't be enumerated). noindex and never cached.
/// </summary>
public sealed class ConfirmationController(
    IBookingReadStore bookingReadStore,
    IBookingTokenService tokenService,
    IContentService content) : PublicController
{
    [HttpGet("{lang:culture}/book/confirmation/{reference}")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Index(string reference, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !tokenService.IsValidConfirmationToken(reference, token))
        {
            return NotFound();
        }

        var booking = await bookingReadStore.GetByReferenceAsync(reference, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        var copy = await content.GetSectionAsync("confirmation", CurrentCulture, cancellationToken);
        var checkInInfo = await content.GetSectionAsync("checkin", CurrentCulture, cancellationToken);

        SetMeta(Meta(
            relativePath: $"book/confirmation/{reference}",
            title: $"Booking {reference} — {BrandName}",
            description: "Your Gaia Skyline booking.",
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Booking", null)],
            noIndex: true));

        return View(new ConfirmationPageViewModel(copy, checkInInfo, booking, token));
    }

    [HttpGet("{lang:culture}/book/confirmation/{reference}/calendar.ics")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Calendar(string reference, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !tokenService.IsValidConfirmationToken(reference, token))
        {
            return NotFound();
        }

        var booking = await bookingReadStore.GetByReferenceAsync(reference, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        var ics = BuildSingleEventIcs(booking);
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"gaia-skyline-{reference}.ics");
    }

    private static string BuildSingleEventIcs(BookingSummaryDto booking)
    {
        static string D(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//GaiaSkyline//v1//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:booking-{booking.ReferenceCode}@gaiaskyline",
            $"DTSTART;VALUE=DATE:{D(booking.CheckIn)}",
            $"DTEND;VALUE=DATE:{D(booking.CheckOut)}",
            "SUMMARY:Gaia Skyline — your stay",
            "STATUS:CONFIRMED",
            "END:VEVENT",
            "END:VCALENDAR",
            string.Empty);
    }
}
