using System.Globalization;
using GaiaSkyline.Application.Pricing;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>Returns a server-computed price breakdown. The client never supplies prices.</summary>
[ApiController]
[Route("api/quote")]
public sealed class QuoteController(IQuoteService quoteService) : ControllerBase
{
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Post([FromBody] QuoteApiRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest();
        }

        try
        {
            var quote = await quoteService.QuoteAsync(
                new QuoteRequest(
                    request.CheckIn,
                    request.CheckOut,
                    new GuestParty(request.Adults, request.Children, request.Infants),
                    request.PromoCode),
                cancellationToken);

            return Ok(QuoteResponse.From(quote));
        }
        catch (PricingException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record QuoteApiRequest(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Infants,
    string? PromoCode = null);

public sealed record NightlyResponse(string Date, decimal Rate);

public sealed record DiscountResponse(string Kind, int Percent, decimal Amount);

public sealed record QuoteResponse(
    int Nights,
    string Currency,
    decimal NightlySubtotal,
    DiscountResponse Discount,
    decimal CleaningFee,
    decimal TouristTax,
    decimal Total,
    int EffectiveMinNights,
    bool PromoRequestedButInvalid,
    IReadOnlyList<NightlyResponse> Nightly)
{
    public static QuoteResponse From(QuoteBreakdown q) => new(
        q.Nights,
        q.Total.Currency,
        q.NightlySubtotal.Amount,
        new DiscountResponse(q.Discount.Kind.ToString(), q.Discount.Percent, q.Discount.Amount.Amount),
        q.CleaningFee.Amount,
        q.TouristTax.Amount,
        q.Total.Amount,
        q.EffectiveMinNights,
        q.PromoRequestedButInvalid,
        q.Nightly.Select(n => new NightlyResponse(n.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), n.Rate.Amount)).ToList());
}
