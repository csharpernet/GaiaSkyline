using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Controllers;

/// <summary>The checkout page: guest details + Stripe Payment Element. noindex and never cached.</summary>
public sealed class CheckoutController(
    IQuoteService quoteService,
    IContentService content,
    IOptions<StripeOptions> stripeOptions) : PublicController
{
    [HttpGet("{lang:culture}/book/checkout")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Index(
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int adults = 2,
        [FromQuery] int children = 0,
        [FromQuery] int infants = 0,
        [FromQuery] string? promo = null,
        CancellationToken cancellationToken = default)
    {
        QuoteBreakdown quote;
        try
        {
            quote = await quoteService.QuoteAsync(
                new QuoteRequest(checkIn, checkOut, new GuestParty(adults, children, infants), promo),
                cancellationToken);
        }
        catch (Exception ex) when (ex is PricingException or ArgumentException)
        {
            // Invalid/expired selection — send the guest back to pick dates.
            return RedirectToAction("Index", "Book", new { lang = CurrentSlug });
        }

        var copy = await content.GetSectionAsync("checkout", CurrentCulture, cancellationToken);
        var amountCents = (long)Math.Round(quote.Total.Amount * 100m, MidpointRounding.AwayFromZero);

        SetMeta(Meta(
            relativePath: "book/checkout",
            title: $"{copy.TextOr("checkout.title", "Checkout")} — {BrandName}",
            description: "Secure checkout for your Gaia Skyline stay.",
            breadcrumbs:
            [
                new Breadcrumb("Home", string.Empty),
                new Breadcrumb("Book", "book"),
                new Breadcrumb(copy.TextOr("checkout.title", "Checkout"), null),
            ],
            noIndex: true));

        return View(new CheckoutPageViewModel(
            copy, quote, stripeOptions.Value.PublishableKey, amountCents,
            checkIn, checkOut, adults, children, infants, promo));
    }
}
