using GaiaSkyline.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Receives Stripe webhooks. The raw body + Stripe-Signature header are verified and processed by
/// <see cref="IStripeWebhookHandler"/>; a handler failure returns 500 so Stripe retries. This is the
/// source of truth for booking confirmation.
/// </summary>
[ApiController]
[Route("webhooks/stripe")]
public sealed class StripeWebhookController(IStripeWebhookHandler handler) : ControllerBase
{
    private const string SignatureHeader = "Stripe-Signature";

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers[SignatureHeader].FirstOrDefault() ?? string.Empty;

        var result = await handler.HandleAsync(payload, signature, cancellationToken);

        return result.Outcome switch
        {
            WebhookOutcome.Ok => Ok(),
            WebhookOutcome.InvalidSignature => BadRequest(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
