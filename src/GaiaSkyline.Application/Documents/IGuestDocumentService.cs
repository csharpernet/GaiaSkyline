namespace GaiaSkyline.Application.Documents;

/// <summary>
/// The branded guest documents (Stage 8). None of these is a tax invoice (fatura); the footer says so in the
/// guest's language. A booking confirms only once payment has succeeded, so the <see cref="Confirmation"/>
/// already carries the payment receipt — a standalone <see cref="PaymentReceipt"/> is produced only when a
/// receipt is wanted on its own. <see cref="CancellationRefund"/> covers a cancellation and/or refund.
/// </summary>
public enum GuestDocumentType
{
    /// <summary>Booking confirmation, including the payment receipt (the normal guest document).</summary>
    Confirmation,

    /// <summary>A payment-focused receipt on its own.</summary>
    PaymentReceipt,

    /// <summary>Cancellation and/or refund receipt, with the policy that applied.</summary>
    CancellationRefund,
}

/// <summary>
/// Generates (and caches) the branded guest PDFs for a booking. A document is produced once and stored via
/// <c>IMediaStorage</c>; it is regenerated only when the booking's document-relevant fields change (Stage 8).
/// </summary>
public interface IGuestDocumentService
{
    /// <summary>The PDF bytes for a booking's document in the guest's language; null when the booking is unknown.</summary>
    Task<byte[]?> GenerateAsync(string referenceCode, GuestDocumentType type, CancellationToken cancellationToken);

    /// <summary>
    /// As <see cref="GenerateAsync"/> but in a specific language — used to produce review samples in every
    /// language regardless of the booking's own language.
    /// </summary>
    Task<byte[]?> GenerateAsync(
        string referenceCode, GuestDocumentType type, string language, CancellationToken cancellationToken);
}
