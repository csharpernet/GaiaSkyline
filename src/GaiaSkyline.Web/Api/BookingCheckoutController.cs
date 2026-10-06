using FluentValidation;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GaiaSkyline.Web.Api;

/// <summary>Creates the booking + PaymentIntent on checkout, and reports booking status for the poll.</summary>
[ApiController]
[Route("api/book")]
public sealed class BookingCheckoutController(
    ICheckoutService checkoutService,
    IBookingReadStore bookingReadStore,
    IBookingTokenService tokenService,
    IValidator<CheckoutRequest> validator) : ControllerBase
{
    [HttpPost("checkout")]
    [EnableRateLimiting("checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UnprocessableEntity(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        TimeOnly? arrival = TimeOnly.TryParse(request.ArrivalEstimateLocal, out var parsed) ? parsed : null;
        var command = new CreateBookingCommand(
            request.CheckIn,
            request.CheckOut,
            new GuestParty(request.Adults, request.Children, request.Infants),
            request.GuestName,
            request.GuestEmail,
            request.GuestPhone,
            request.GuestCountry,
            request.Language,
            request.PromoCode,
            arrival,
            request.SpecialRequests,
            request.CreateAccount,
            // Stage 8 Part A: the 30-day referral cookie attributes the booking when no partner code is
            // typed (ADR 0019).
            ReferralCode: Request.Cookies[GaiaSkyline.Web.Middleware.PartnerRefMiddleware.RefCookieName]);

        try
        {
            var result = await checkoutService.StartAsync(command, cancellationToken);
            var slug = SupportedCultures.SlugForCulture(request.Language);
            var returnUrl =
                $"{Request.Scheme}://{Request.Host}/{slug}/book/confirmation/{result.BookingReference}" +
                $"?token={Uri.EscapeDataString(result.ConfirmationToken)}";
            return Ok(new { clientSecret = result.ClientSecret, returnUrl });
        }
        catch (DatesUnavailableException)
        {
            return Conflict(new { error = "Those dates were just taken." });
        }
        catch (Exception ex) when (ex is PricingException or ArgumentException)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    [HttpGet("status/{reference}")]
    [EnableRateLimiting("quote")]
    public async Task<IActionResult> Status(string reference, [FromQuery] string token, CancellationToken cancellationToken)
    {
        if (!tokenService.IsValidConfirmationToken(reference, token))
        {
            return NotFound();
        }

        var booking = await bookingReadStore.GetByReferenceAsync(reference, cancellationToken);
        return booking is null ? NotFound() : Ok(new { status = booking.Status.ToString() });
    }
}
