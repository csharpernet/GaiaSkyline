using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Bookings;

/// <summary>
/// A direct booking of the apartment. Created in <see cref="BookingStatus.AwaitingPayment"/> with its
/// nights held (see <see cref="BookingDateOccupancy"/>); the Stripe webhook drives it to
/// <see cref="BookingStatus.Confirmed"/>. Monetary fields are snapshots taken at quote time — the
/// server never trusts client-supplied prices. All amounts are EUR, stored as minor units (cents).
/// </summary>
public sealed class Booking : Entity<BookingId>
{
    private const int MaxOccupancy = 6;

    // Required by EF Core's materialization.
    private Booking()
    {
    }

    public Booking(
        BookingId id,
        string referenceCode,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children,
        int infants,
        string guestName,
        string guestEmail,
        string guestPhone,
        string guestCountry,
        string guestLanguage,
        Money nightlyRateSnapshot,
        Money subtotal,
        Money discountAmount,
        Money cleaningFee,
        Money touristTax,
        Money total,
        DateTime createdAtUtc,
        PromoCodeId? promoCodeId = null,
        bool accountCreationRequested = false,
        TimeOnly? arrivalEstimateLocal = null,
        string? specialRequests = null,
        Guid? guestUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestName);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestPhone);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestCountry);
        ArgumentException.ThrowIfNullOrWhiteSpace(guestLanguage);

        if (checkOut <= checkIn)
        {
            throw new ArgumentException("Check-out must be after check-in.", nameof(checkOut));
        }

        ValidateGuests(adults, children, infants);
        EnsureTotalAddsUp(subtotal, discountAmount, cleaningFee, touristTax, total);

        Id = id;
        ReferenceCode = referenceCode.Trim().ToUpperInvariant();
        CheckIn = checkIn;
        CheckOut = checkOut;
        Adults = adults;
        Children = children;
        Infants = infants;
        GuestName = guestName.Trim();
        GuestEmail = guestEmail.Trim();
        GuestPhone = guestPhone.Trim();
        GuestCountry = guestCountry.Trim();
        GuestLanguage = guestLanguage.Trim();
        NightlyRateSnapshot = nightlyRateSnapshot;
        Subtotal = subtotal;
        DiscountAmount = discountAmount;
        CleaningFee = cleaningFee;
        TouristTax = touristTax;
        Total = total;
        Status = BookingStatus.AwaitingPayment;
        PromoCodeId = promoCodeId;
        AccountCreationRequested = accountCreationRequested;
        ArrivalEstimateLocal = arrivalEstimateLocal;
        SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim();
        GuestUserId = guestUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public string ReferenceCode { get; private set; } = null!;

    public DateOnly CheckIn { get; private set; }

    public DateOnly CheckOut { get; private set; }

    /// <summary>Number of nights (check-out minus check-in).</summary>
    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    public int Adults { get; private set; }

    public int Children { get; private set; }

    public int Infants { get; private set; }

    public string GuestName { get; private set; } = null!;

    public string GuestEmail { get; private set; } = null!;

    public string GuestPhone { get; private set; } = null!;

    public string GuestCountry { get; private set; } = null!;

    /// <summary>The guest's language (culture name), used to localise emails and the confirmation page.</summary>
    public string GuestLanguage { get; private set; } = null!;

    /// <summary>Set in Stage 6 when a booking is linked to a guest account.</summary>
    public Guid? GuestUserId { get; private set; }

    public Money NightlyRateSnapshot { get; private set; }

    public Money Subtotal { get; private set; }

    public Money DiscountAmount { get; private set; }

    public Money CleaningFee { get; private set; }

    public Money TouristTax { get; private set; }

    public Money Total { get; private set; }

    public BookingStatus Status { get; private set; }

    public PromoCodeId? PromoCodeId { get; private set; }

    public string? StripeCustomerId { get; private set; }

    public string? StripePaymentIntentId { get; private set; }

    /// <summary>The Stripe payment method type actually used (e.g. "card", "multibanco"); set on confirmation.</summary>
    public string? PaymentMethodType { get; private set; }

    public string? MultibancoEntity { get; private set; }

    public string? MultibancoReference { get; private set; }

    /// <summary>When a Multibanco voucher expires (taken from Stripe, never hardcoded).</summary>
    public DateTime? PaymentExpiresAtUtc { get; private set; }

    public TimeOnly? ArrivalEstimateLocal { get; private set; }

    public string? SpecialRequests { get; private set; }

    /// <summary>Whether the guest asked to create an account (acted on in Stage 6).</summary>
    public bool AccountCreationRequested { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// When the owner last confirmed this booking's change was mirrored in the management system
    /// (Hostify). Null = never acknowledged. A later status change (confirm/cancel) past this time puts
    /// the booking back on the manual-sync to-do until acknowledged again. See the Stage 5 preface.
    /// </summary>
    public DateTime? ExternalChannelSyncedAtUtc { get; private set; }

    public string? ExternalChannelSyncNote { get; private set; }

    /// <summary>Associates the Stripe customer used for this booking.</summary>
    public void AttachStripeCustomer(string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        StripeCustomerId = customerId;
    }

    /// <summary>Associates the Stripe PaymentIntent that will collect this booking's payment.</summary>
    public void AttachPaymentIntent(string paymentIntentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentIntentId);
        StripePaymentIntentId = paymentIntentId;
    }

    /// <summary>Records the Multibanco voucher (entity, reference, expiry) returned by Stripe.</summary>
    public void SetMultibancoVoucher(string entity, string reference, DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        MultibancoEntity = entity.Trim();
        MultibancoReference = reference.Trim();
        PaymentExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Confirms the booking after a successful payment (AwaitingPayment → Confirmed).</summary>
    public void ConfirmPayment(string? paymentMethodType, DateTime confirmedAtUtc)
    {
        Transition(BookingStatus.Confirmed);
        PaymentMethodType = string.IsNullOrWhiteSpace(paymentMethodType) ? PaymentMethodType : paymentMethodType.Trim();
        ConfirmedAtUtc = confirmedAtUtc;
    }

    /// <summary>Cancels the booking and records why (→ Cancelled).</summary>
    public void Cancel(string? reason, DateTime cancelledAtUtc)
    {
        Transition(BookingStatus.Cancelled);
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>Marks the booking fully refunded (→ Refunded).</summary>
    public void MarkRefunded() => Transition(BookingStatus.Refunded);

    /// <summary>Marks the booking partially refunded (→ PartiallyRefunded).</summary>
    public void MarkPartiallyRefunded() => Transition(BookingStatus.PartiallyRefunded);

    /// <summary>Marks the guest as arrived (→ CheckedIn).</summary>
    public void CheckInGuest() => Transition(BookingStatus.CheckedIn);

    /// <summary>Marks the stay finished (→ Completed).</summary>
    public void Complete() => Transition(BookingStatus.Completed);

    /// <summary>Appends an internal operations note.</summary>
    public void SetNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    /// <summary>Records that the owner mirrored this booking's current state in the management system.</summary>
    public void MarkExternalChannelSynced(DateTime atUtc, string? note)
    {
        ExternalChannelSyncedAtUtc = atUtc;
        ExternalChannelSyncNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private void Transition(BookingStatus to)
    {
        BookingStatusTransitions.EnsureCanTransition(Status, to);
        Status = to;
    }

    private static void ValidateGuests(int adults, int children, int infants)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(adults, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(children);
        ArgumentOutOfRangeException.ThrowIfNegative(infants);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(infants, 1);

        var counted = adults + children; // infants are excluded from the occupancy cap
        ArgumentOutOfRangeException.ThrowIfGreaterThan(counted, MaxOccupancy);
    }

    private static void EnsureTotalAddsUp(Money subtotal, Money discount, Money cleaning, Money tax, Money total)
    {
        var expected = subtotal - discount + cleaning + tax;
        if (expected.Amount != total.Amount)
        {
            throw new BookingTotalMismatchException(expected.Amount, total.Amount);
        }
    }
}
