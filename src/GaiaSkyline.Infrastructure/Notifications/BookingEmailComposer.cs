using System.Globalization;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Documents;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>
/// Builds a localized <see cref="EmailMessage"/> for a booking email. Copy comes from the
/// <c>email.{kind}.{subject|body}</c> content blocks in the recipient's language (guest emails use
/// the guest's language; owner/dispute emails are English), with placeholders filled from the booking.
/// Built-in English defaults are used when a content block is missing, so emails always render.
/// </summary>
internal sealed class BookingEmailComposer(
    IContentService content,
    IBookingTokenService tokens,
    IGuestDocumentService guestDocuments,
    IOptionsSnapshot<EmailOptions> options)
{
    private readonly EmailOptions _options = options.Value;

    public async Task<EmailMessage> ComposeAsync(Booking booking, BookingEmailKind kind, CancellationToken cancellationToken)
    {
        // Owner, dispute and property-manager emails are English; guest emails use the guest's language.
        var english = kind is BookingEmailKind.OwnerNotification
            or BookingEmailKind.DisputeAlert
            or BookingEmailKind.PropertyManager;
        var language = english ? "en" : booking.GuestLanguage;
        var payload = await content.GetSectionAsync("email", language, cancellationToken);

        var keyBase = $"email.{KindKey(kind)}";
        var (defaultSubject, defaultBody) = Defaults[kind];
        var subjectTemplate = Resolve(payload, $"{keyBase}.subject", defaultSubject);
        var bodyTemplate = Resolve(payload, $"{keyBase}.body", defaultBody);

        var replacements = BuildReplacements(booking, language);
        var subject = Apply(subjectTemplate, replacements);
        var innerHtml = Apply(bodyTemplate, replacements);

        // The owner acts on each booking by hand in Hostify until iCal sync is connected: lead with it.
        if (kind == BookingEmailKind.OwnerNotification)
        {
            var verb = IsCancelled(booking.Status) ? "unblock" : "block";
            innerHtml =
                $"<p style=\"font-weight:bold;color:#A04A28;\">Action needed: ask the management company to {verb} " +
                $"{replacements["{checkIn}"]} – {replacements["{checkOut}"]} in Hostify.</p>" + innerHtml;
        }

        var body = WrapInLayout(innerHtml);

        var (toAddress, toName) = kind switch
        {
            BookingEmailKind.PropertyManager => (string.Empty, "Property manager"),
            _ when english => (_options.OwnerAddress, _options.FromName),
            _ => (booking.GuestEmail, booking.GuestName),
        };

        // The property-manager copy carries the booking as an .ics (no payment data); the guest's
        // confirmation/refund/cancellation emails carry the matching branded PDF (Stage 8).
        IReadOnlyList<EmailAttachment>? attachments = null;
        if (kind == BookingEmailKind.PropertyManager)
        {
            attachments = [new EmailAttachment($"gaia-skyline-{booking.ReferenceCode}.ics", "text/calendar", BuildIcs(booking))];
        }
        else if (GuestDocumentFor(kind) is { } docType)
        {
            var pdf = await guestDocuments.GenerateAsync(booking.ReferenceCode, docType, cancellationToken);
            if (pdf is not null)
            {
                var name = docType == GuestDocumentType.CancellationRefund ? "cancellation" : "confirmation";
                attachments = [new EmailAttachment($"gaia-skyline-{name}-{booking.ReferenceCode}.pdf", "application/pdf", pdf)];
            }
        }

        return new EmailMessage(toAddress, toName, subject, body, attachments);
    }

    // The guest emails that warrant a PDF, and which document each attaches. A refund and a cancellation both
    // send the cancellation/refund receipt; the Multibanco and payment-expired notices carry none.
    private static GuestDocumentType? GuestDocumentFor(BookingEmailKind kind) => kind switch
    {
        BookingEmailKind.Confirmation => GuestDocumentType.Confirmation,
        BookingEmailKind.Refund => GuestDocumentType.CancellationRefund,
        BookingEmailKind.Cancellation => GuestDocumentType.CancellationRefund,
        _ => null,
    };

    private static bool IsCancelled(BookingStatus status) =>
        status is BookingStatus.Cancelled or BookingStatus.Refunded or BookingStatus.PartiallyRefunded;

    private static byte[] BuildIcs(Booking booking)
    {
        static string D(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var ics = string.Join("\r\n",
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//GaiaSkyline//v1//EN", "CALSCALE:GREGORIAN", "METHOD:PUBLISH",
            "BEGIN:VEVENT", $"UID:booking-{booking.ReferenceCode}@gaiaskyline",
            $"DTSTART;VALUE=DATE:{D(booking.CheckIn)}", $"DTEND;VALUE=DATE:{D(booking.CheckOut)}",
            "SUMMARY:Gaia Skyline — direct booking", "END:VEVENT", "END:VCALENDAR", string.Empty);
        return System.Text.Encoding.UTF8.GetBytes(ics);
    }

    private static string Resolve(ContentPayload payload, string key, string fallback) =>
        payload.Items.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.Text)
            ? value.Text!
            : fallback;

    private Dictionary<string, string> BuildReplacements(Booking booking, string language)
    {
        var culture = ResolveCulture(language);
        var slug = language.ToLowerInvariant();
        var token = tokens.CreateConfirmationToken(booking.ReferenceCode);
        var confirmationUrl =
            $"{_options.SiteBaseUrl.TrimEnd('/')}/{slug}/book/confirmation/{booking.ReferenceCode}" +
            $"?token={Uri.EscapeDataString(token)}";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{reference}"] = booking.ReferenceCode,
            ["{guestName}"] = booking.GuestName,
            ["{guestEmail}"] = booking.GuestEmail,
            ["{guestPhone}"] = booking.GuestPhone,
            ["{guestCountry}"] = booking.GuestCountry,
            ["{status}"] = booking.Status.ToString(),
            ["{arrivalEstimate}"] = booking.ArrivalEstimateLocal?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "—",
            ["{specialRequests}"] = string.IsNullOrWhiteSpace(booking.SpecialRequests) ? "—" : booking.SpecialRequests,
            ["{nights}"] = booking.Nights.ToString(CultureInfo.InvariantCulture),
            ["{checkIn}"] = booking.CheckIn.ToString("dd MMM yyyy", culture),
            ["{checkOut}"] = booking.CheckOut.ToString("dd MMM yyyy", culture),
            ["{adults}"] = booking.Adults.ToString(CultureInfo.InvariantCulture),
            ["{children}"] = booking.Children.ToString(CultureInfo.InvariantCulture),
            ["{infants}"] = booking.Infants.ToString(CultureInfo.InvariantCulture),
            ["{subtotal}"] = Euro(booking.Subtotal.Amount),
            ["{discount}"] = Euro(booking.DiscountAmount.Amount),
            ["{cleaningFee}"] = Euro(booking.CleaningFee.Amount),
            ["{touristTax}"] = Euro(booking.TouristTax.Amount),
            ["{total}"] = Euro(booking.Total.Amount),
            ["{multibancoEntity}"] = booking.MultibancoEntity ?? string.Empty,
            ["{multibancoReference}"] = booking.MultibancoReference ?? string.Empty,
            ["{paymentExpiry}"] = booking.PaymentExpiresAtUtc?.ToString("dd MMM yyyy HH:mm 'UTC'", culture) ?? string.Empty,
            ["{confirmationUrl}"] = confirmationUrl,
        };
    }

    private static string Apply(string template, Dictionary<string, string> replacements)
    {
        foreach (var (token, value) in replacements)
        {
            template = template.Replace(token, value, StringComparison.Ordinal);
        }

        return template;
    }

    private static string Euro(decimal amount) => "€" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static CultureInfo ResolveCulture(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static string KindKey(BookingEmailKind kind) => kind switch
    {
        BookingEmailKind.Confirmation => "confirmation",
        BookingEmailKind.MultibancoReference => "multibanco_reference",
        BookingEmailKind.PaymentExpired => "payment_expired",
        BookingEmailKind.Refund => "refund",
        BookingEmailKind.Cancellation => "cancellation",
        BookingEmailKind.OwnerNotification => "owner_notification",
        BookingEmailKind.DisputeAlert => "dispute_alert",
        BookingEmailKind.PropertyManager => "property_manager",
        _ => "confirmation",
    };

    private static string WrapInLayout(string innerHtml) =>
        "<!doctype html><html><body style=\"font-family:Arial,Helvetica,sans-serif;color:#0F1417;\">" +
        "<div style=\"max-width:560px;margin:0 auto;padding:24px;\">" +
        "<h1 style=\"font-size:20px;color:#A04A28;\">Gaia Skyline</h1>" +
        innerHtml +
        "<hr style=\"border:none;border-top:1px solid #D9D2C5;margin:24px 0;\" />" +
        "<p style=\"font-size:12px;color:#2E4F60;\">Gaia Skyline Apartment · Vila Nova de Gaia · AL 175890/AL</p>" +
        "</div></body></html>";

    // English defaults (subject, body). Content blocks override these per language.
    private static readonly Dictionary<BookingEmailKind, (string Subject, string Body)> Defaults = new()
    {
        [BookingEmailKind.Confirmation] = (
            "Your Gaia Skyline booking {reference} is confirmed",
            "<p>Hi {guestName},</p><p>Your stay is confirmed — reference <strong>{reference}</strong>.</p>" +
            "<ul><li>Check-in: {checkIn}</li><li>Check-out: {checkOut}</li><li>Nights: {nights}</li>" +
            "<li>Total paid: {total}</li></ul><p><a href=\"{confirmationUrl}\">View your booking</a></p>"),
        [BookingEmailKind.MultibancoReference] = (
            "Your Multibanco reference for booking {reference}",
            "<p>Hi {guestName},</p><p>To confirm booking <strong>{reference}</strong>, pay by Multibanco:</p>" +
            "<ul><li>Entity: <strong>{multibancoEntity}</strong></li><li>Reference: <strong>{multibancoReference}</strong></li>" +
            "<li>Amount: {total}</li><li>Pay before: {paymentExpiry}</li></ul>" +
            "<p>We'll confirm your booking automatically once payment is received.</p>"),
        [BookingEmailKind.PaymentExpired] = (
            "Your Gaia Skyline booking {reference} has expired",
            "<p>Hi {guestName},</p><p>We didn't receive payment in time, so booking {reference} has been released. " +
            "You're very welcome to book again.</p>"),
        [BookingEmailKind.Refund] = (
            "Refund processed for booking {reference}",
            "<p>Hi {guestName},</p><p>A refund has been processed for booking {reference}.</p>"),
        [BookingEmailKind.Cancellation] = (
            "Your Gaia Skyline booking {reference} is cancelled",
            "<p>Hi {guestName},</p><p>Booking {reference} has been cancelled.</p>"),
        [BookingEmailKind.OwnerNotification] = (
            "New booking {reference} — {checkIn} to {checkOut}",
            "<p>New confirmed booking:</p><ul><li>Reference: {reference}</li><li>Guest: {guestName}</li>" +
            "<li>Dates: {checkIn} – {checkOut} ({nights} nights)</li>" +
            "<li>Guests: {adults} adults, {children} children, {infants} infants</li><li>Total: {total}</li></ul>"),
        [BookingEmailKind.DisputeAlert] = (
            "Dispute opened on booking {reference}",
            "<p>A payment dispute has been opened for booking {reference} ({guestName}, {total}). " +
            "Review it in the Stripe dashboard immediately.</p>"),
        [BookingEmailKind.PropertyManager] = (
            "Direct booking {reference} — please block {checkIn} to {checkOut} in Hostify",
            "<p><strong>Please block these dates in Hostify.</strong></p>" +
            "<ul><li>Reference: {reference}</li><li>Status: {status}</li>" +
            "<li>Dates: {checkIn} – {checkOut} ({nights} nights)</li>" +
            "<li>Guests: {adults} adults, {children} children, {infants} infants</li>" +
            "<li>Guest: {guestName}</li><li>Email: {guestEmail}</li><li>Phone: {guestPhone}</li>" +
            "<li>Country: {guestCountry}</li><li>Arrival estimate: {arrivalEstimate}</li>" +
            "<li>Special requests: {specialRequests}</li></ul>" +
            "<p>The booking is attached as an .ics.</p>"),
    };
}
