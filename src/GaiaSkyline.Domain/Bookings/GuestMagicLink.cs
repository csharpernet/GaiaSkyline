namespace GaiaSkyline.Domain.Bookings;

/// <summary>
/// A single-use, time-limited token that lets a guest without an account open one booking. Expiry and
/// consumption are tracked in the database so the link genuinely works only once, within its window.
/// </summary>
public sealed class GuestMagicLink
{
    // Required by EF Core's materialization.
    private GuestMagicLink()
    {
    }

    public GuestMagicLink(Guid id, string bookingReference, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        Id = id;
        BookingReference = bookingReference.Trim().ToUpperInvariant();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public string BookingReference { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public bool IsUsable(DateTime nowUtc) => ConsumedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void Consume(DateTime atUtc) => ConsumedAtUtc = atUtc;
}
