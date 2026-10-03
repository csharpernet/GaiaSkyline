namespace GaiaSkyline.Application.Payments;

/// <summary>Outcome of handling a Stripe webhook, mapped to an HTTP status by the controller.</summary>
public enum WebhookOutcome
{
    /// <summary>Handled (or a duplicate that was ignored) — return 200.</summary>
    Ok,

    /// <summary>Signature verification failed — return 400.</summary>
    InvalidSignature,

    /// <summary>Handler failed; record the error and return 500 so Stripe retries.</summary>
    Error,
}

public sealed record WebhookResult(WebhookOutcome Outcome, string? Message = null);

/// <summary>
/// Verifies and processes Stripe webhook events. The <see cref="Domain.Payments.StripeEventLog"/>
/// makes processing idempotent (a duplicate delivery is a no-op); the webhook is the source of truth
/// for booking confirmation, not the browser redirect.
/// </summary>
public interface IStripeWebhookHandler
{
    Task<WebhookResult> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken);
}
