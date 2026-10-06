namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>
/// A cached, rendered guest PDF (Stage 8). Unlike public website media, the guest documents carry payment
/// data (card last-4, Stripe references, refund amounts), so they are deliberately NOT stored through
/// <c>IMediaStorage</c> — that seam is write-only and backed by the public <c>wwwroot/media</c> folder, which
/// would expose a receipt at a guessable URL that bypasses the booking-access cookie. They live here in the
/// database instead, served only through the authenticated/cookie-gated controller actions.
/// <para>
/// A row is keyed by booking reference, document type and language. It is regenerated only when the booking's
/// material content changes, detected by <see cref="SourceHash"/> — a fingerprint of everything the document
/// renders (its issue date excepted, so an unrelated re-download reuses the stored bytes).
/// </para>
/// </summary>
internal sealed class BookingDocument
{
    private BookingDocument()
    {
        // EF Core.
    }

    public BookingDocument(
        string bookingReference, string documentType, string language, string sourceHash, byte[] content, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        BookingReference = bookingReference;
        DocumentType = documentType;
        Language = language;
        SourceHash = sourceHash;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string BookingReference { get; private set; } = string.Empty;

    /// <summary>The <c>GuestDocumentType</c> name (Confirmation / PaymentReceipt / CancellationRefund).</summary>
    public string DocumentType { get; private set; } = string.Empty;

    public string Language { get; private set; } = string.Empty;

    /// <summary>SHA-256 (hex) of the rendered content; a mismatch means the booking changed — regenerate.</summary>
    public string SourceHash { get; private set; } = string.Empty;

    public byte[] Content { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Replaces the stored bytes after the booking's content changed.</summary>
    public void Replace(string sourceHash, byte[] content, DateTime createdAtUtc)
    {
        SourceHash = sourceHash;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }
}
