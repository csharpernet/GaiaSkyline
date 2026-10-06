using GaiaSkyline.Application.Documents;

namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>One line in the price-breakdown table (amount pre-formatted for the guest's culture).</summary>
public sealed record PriceLine(string Label, string Amount, bool Negative = false);

/// <summary>The payment section (card/wallet/Multibanco), null when the booking is not yet paid.</summary>
public sealed record PaymentSection(string Method, string PaidOn, string? StripeReference, string Amount, string Status);

/// <summary>The refund section on a cancellation/refund document.</summary>
public sealed record RefundSection(string Amount, string Date, string? Reason);

/// <summary>
/// Everything a guest PDF renders, fully assembled and localised to the guest's language with culture-correct
/// dates and amounts (Stage 8). The renderer only lays this out — no business logic.
/// </summary>
public sealed record GuestDocumentModel(
    GuestDocumentType Type,
    DocumentStrings S,
    string Title,
    string Reference,
    string IssueDate,
    string PropertyName,
    string PropertyRegistration,
    string CheckIn,
    string CheckOut,
    string Nights,
    string Guests,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    string GuestCountry,
    IReadOnlyList<PriceLine> PriceLines,
    string Total,
    PaymentSection? Payment,
    RefundSection? Refund,
    string CancellationPolicy,
    string ArrivalCheckIn,
    string ArrivalLateFee,
    string ArrivalContact,
    byte[] QrPng,
    string QrCaption,
    string FooterNotTaxInvoice,
    string FooterContact);

/// <summary>
/// The localised section labels and fixed phrases on the documents. Every value has an English default so a
/// document renders correctly even before the <c>document.*</c> content blocks are translated; a present
/// content-block value overrides the default (mirrors the booking-email composer's fallback approach).
/// </summary>
public sealed record DocumentStrings(
    string IssuedLabel,
    string StaySummary,
    string CheckInLabel,
    string CheckOutLabel,
    string CheckInTime,
    string CheckOutTime,
    string NightsLabel,
    string GuestsLabel,
    string GuestDetails,
    string EmailLabel,
    string PhoneLabel,
    string CountryLabel,
    string PriceBreakdown,
    string AccommodationLabel,
    string DiscountLabel,
    string CleaningLabel,
    string TouristTaxLabel,
    string TotalLabel,
    string PaymentLabel,
    string MethodLabel,
    string PaidOnLabel,
    string AmountLabel,
    string StatusLabel,
    string RefundLabel,
    string RefundedLabel,
    string RefundDateLabel,
    string ReasonLabel,
    string CancellationPolicyLabel,
    string ArrivalLabel)
{
    /// <summary>English defaults; <paramref name="get"/> supplies a content-block override for a key, or null.</summary>
    public static DocumentStrings Resolve(Func<string, string?> get)
    {
        string V(string key, string fallback)
        {
            var value = get(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value!;
        }

        return new DocumentStrings(
            V("document.label.issued", "Issued"),
            V("document.label.stay", "Your stay"),
            V("document.label.checkin", "Check-in"),
            V("document.label.checkout", "Check-out"),
            V("document.label.checkin_time", "from 16:00"),
            V("document.label.checkout_time", "until 10:00"),
            V("document.label.nights", "Nights"),
            V("document.label.guests", "Guests"),
            V("document.label.guest_details", "Guest details"),
            V("document.label.email", "Email"),
            V("document.label.phone", "Phone"),
            V("document.label.country", "Country"),
            V("document.label.price", "Price breakdown"),
            V("document.label.accommodation", "Accommodation"),
            V("document.label.discount", "Discount"),
            V("document.label.cleaning", "Cleaning fee"),
            V("document.label.tourist_tax", "Tourist tax"),
            V("document.label.total", "Total"),
            V("document.label.payment", "Payment"),
            V("document.label.method", "Method"),
            V("document.label.paid_on", "Paid on"),
            V("document.label.amount", "Amount"),
            V("document.label.status", "Status"),
            V("document.label.refund", "Refund"),
            V("document.label.refunded", "Amount refunded"),
            V("document.label.refund_date", "Refunded on"),
            V("document.label.reason", "Reason"),
            V("document.label.cancellation_policy", "Cancellation policy"),
            V("document.label.arrival", "Before you arrive"));
    }
}
