using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Documents;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>
/// Builds the branded guest PDFs from a booking (Stage 8): loads the booking, property, cancellation policy
/// and the localised <c>document.*</c> content, assembles a <see cref="GuestDocumentModel"/> with
/// culture-correct dates and amounts, and renders it. None of these documents is a tax invoice (fatura).
/// Rendered output is cached in <see cref="BookingDocument"/> and reused until the booking's content changes.
/// </summary>
internal sealed class GuestDocumentService(
    AppDbContext dbContext,
    IContentService content,
    IOptions<EmailOptions> emailOptions,
    TimeProvider clock) : IGuestDocumentService
{
    public Task<byte[]?> GenerateAsync(string referenceCode, GuestDocumentType type, CancellationToken cancellationToken) =>
        GenerateAsync(referenceCode, type, language: null, cancellationToken);

    public async Task<byte[]?> GenerateAsync(
        string referenceCode, GuestDocumentType type, string? language, CancellationToken cancellationToken)
    {
        var reference = referenceCode.Trim().ToUpperInvariant();
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.ReferenceCode == reference, cancellationToken);
        if (booking is null)
        {
            return null;
        }

        var lang = Normalize(language ?? booking.GuestLanguage);
        DocumentTheme.EnsureInitialised();

        var section = await content.GetSectionAsync("document", lang, cancellationToken);
        string? Get(string key) => section.Items.TryGetValue(key, out var value) ? value.Text : null;
        var s = DocumentStrings.Resolve(Get);

        var property = await content.GetPropertyAsync(cancellationToken);
        var policy = await dbContext.CancellationPolicies.AsNoTracking()
            .Include(p => p.Tiers).FirstOrDefaultAsync(cancellationToken);

        var culture = CultureFor(lang);
        var model = BuildModel(booking, type, s, Get, property, policy, culture, lang);

        // Documents are cached (Stage 8): rendered once, stored in the database, reused until the booking's
        // material content changes. The fingerprint covers everything the PDF renders bar the issue date, so a
        // plain re-download reuses the stored bytes while any booking/content edit regenerates. This is a
        // terminal, post-commit operation at every call site, so writing the cache on the ambient context
        // only ever persists this row.
        var hash = Fingerprint(model);
        var typeKey = type.ToString();
        var existing = await dbContext.Set<BookingDocument>()
            .FirstOrDefaultAsync(
                d => d.BookingReference == reference && d.DocumentType == typeKey && d.Language == lang,
                cancellationToken);
        if (existing is not null && existing.SourceHash == hash)
        {
            return existing.Content;
        }

        var bytes = new GuestDocumentRenderer(model).GeneratePdf();
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            dbContext.Set<BookingDocument>().Add(new BookingDocument(reference, typeKey, lang, hash, bytes, now));
        }
        else
        {
            existing.Replace(hash, bytes, now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request rendered and stored the same document first; our bytes are equivalent.
        }

        return bytes;
    }

    /// <summary>
    /// A stable SHA-256 over everything the document renders except its issue date, so the cache is reused for
    /// an unchanged booking (even on a later day) but regenerates the moment any rendered value changes — a
    /// price, a status, a refund, or an edited <c>document.*</c> label. The QR PNG is included, so a changed
    /// manage-booking URL also invalidates.
    /// </summary>
    private static string Fingerprint(GuestDocumentModel m)
    {
        var sb = new StringBuilder();
        void Add(string? value) => sb.Append(value).Append('\n');

        Add(m.Type.ToString());
        Add(m.Title);
        Add(m.Reference);
        Add(m.PropertyName);
        Add(m.PropertyRegistration);
        Add(m.CheckIn);
        Add(m.CheckOut);
        Add(m.Nights);
        Add(m.Guests);
        Add(m.GuestName);
        Add(m.GuestEmail);
        Add(m.GuestPhone);
        Add(m.GuestCountry);
        Add(m.Total);
        foreach (var line in m.PriceLines)
        {
            Add($"{line.Label}={(line.Negative ? "-" : "+")}{line.Amount}");
        }

        if (m.Payment is { } p)
        {
            Add($"pay:{p.Method}|{p.PaidOn}|{p.StripeReference}|{p.Amount}|{p.Status}");
        }

        if (m.Refund is { } r)
        {
            Add($"refund:{r.Amount}|{r.Date}|{r.Reason}");
        }

        Add(m.CancellationPolicy);
        Add(m.ArrivalCheckIn);
        Add(m.ArrivalLateFee);
        Add(m.ArrivalContact);
        Add(m.QrCaption);
        Add(m.FooterNotTaxInvoice);
        Add(m.FooterContact);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToHexStringLower(SHA256.HashData([.. bytes, .. m.QrPng]));
    }

    private GuestDocumentModel BuildModel(
        Booking booking,
        GuestDocumentType type,
        DocumentStrings s,
        Func<string, string?> get,
        PropertyDto? property,
        Domain.Pricing.CancellationPolicy? policy,
        CultureInfo culture,
        string lang)
    {
        string Money(Money money) => money.Amount.ToString("N2", culture) + " €";
        string Date(DateOnly date) => date.ToString("d MMMM yyyy", culture);
        string DateT(DateTime utc) => utc.ToString("d MMMM yyyy", culture);

        var title = type switch
        {
            GuestDocumentType.Confirmation => get("document.confirmation.title") ?? "Booking confirmation & receipt",
            GuestDocumentType.PaymentReceipt => get("document.receipt.title") ?? "Payment receipt",
            _ => get("document.cancellation.title") ?? "Cancellation & refund receipt",
        };

        var priceLines = new List<PriceLine>
        {
            new($"{s.AccommodationLabel} ({booking.Nights} × {Money(booking.NightlyRateSnapshot)})", Money(booking.Subtotal)),
        };
        if (booking.DiscountAmount.Amount > 0)
        {
            priceLines.Add(new PriceLine(s.DiscountLabel, Money(booking.DiscountAmount), Negative: true));
        }

        if (booking.CleaningFee.Amount > 0)
        {
            priceLines.Add(new PriceLine(s.CleaningLabel, Money(booking.CleaningFee)));
        }

        if (booking.TouristTax.Amount > 0)
        {
            priceLines.Add(new PriceLine(s.TouristTaxLabel, Money(booking.TouristTax)));
        }

        // A booking confirms only once paid, so the confirmation/receipt carries the payment. The
        // cancellation/refund document omits it and shows the refund instead.
        PaymentSection? payment = type == GuestDocumentType.CancellationRefund || booking.ConfirmedAtUtc is null
            ? null
            : new PaymentSection(
                PaymentMethodDisplay(booking),
                booking.ConfirmedAtUtc is { } paid ? DateT(paid) : "—",
                booking.StripePaymentIntentId,
                Money(booking.Total),
                HumaniseStatus(booking.Status, get));

        RefundSection? refund = type == GuestDocumentType.CancellationRefund && booking.RefundedAmount.Amount > 0
            ? new RefundSection(
                Money(booking.RefundedAmount),
                booking.CancelledAtUtc is { } c ? DateT(c) : DateT(clock.GetUtcNow().UtcDateTime),
                booking.CancellationReason)
            : null;

        var slug = lang.ToLowerInvariant();
        var baseUrl = emailOptions.Value.SiteBaseUrl.TrimEnd('/');
        var manageUrl = $"{baseUrl}/{slug}/account/magic-link?reference={Uri.EscapeDataString(booking.ReferenceCode)}";

        return new GuestDocumentModel(
            type,
            s,
            title,
            booking.ReferenceCode,
            DateT(clock.GetUtcNow().UtcDateTime),
            property?.Name ?? "Gaia Skyline",
            $"AL {property?.RegistrationCode ?? "175890/AL"}",
            Date(booking.CheckIn),
            Date(booking.CheckOut),
            booking.Nights.ToString(CultureInfo.InvariantCulture),
            GuestsText(booking, get),
            booking.GuestName,
            booking.GuestEmail,
            booking.GuestPhone,
            booking.GuestCountry,
            priceLines,
            Money(booking.Total),
            payment,
            refund,
            CancellationPolicyText(get, policy, culture),
            get("document.arrival.checkin") ?? "Check-in is from 16:00 with a smart lock; your personal entry code arrives by email on the morning of arrival.",
            get("document.arrival.late_fee") ?? "Arriving after 23:00? A €30 cash late check-in fee applies.",
            get("document.arrival.contact") ?? "Questions? Message your host any time — we usually reply within the hour.",
            QrCode.Png(manageUrl),
            get("document.qr.caption") ?? "Scan to manage your booking",
            get("document.footer.not_tax_invoice")
                ?? "This document is a booking confirmation/receipt and is not a tax invoice (fatura).",
            get("document.footer.contact") ?? $"{emailOptions.Value.FromAddress} · Vila Nova de Gaia, Portugal");
    }

    private static string GuestsText(Booking booking, Func<string, string?> get)
    {
        var adults = get("document.label.adults") ?? "adults";
        var children = get("document.label.children") ?? "children";
        var infants = get("document.label.infants") ?? "infants";
        return $"{booking.Adults} {adults}, {booking.Children} {children}, {booking.Infants} {infants}";
    }

    private static string PaymentMethodDisplay(Booking booking) => booking.PaymentMethodType switch
    {
        null or "" => "—",
        "card" => "Card",
        "multibanco" => booking.MultibancoReference is { } r
            ? $"Multibanco (Entity {booking.MultibancoEntity}, Ref {r})"
            : "Multibanco",
        "apple_pay" => "Apple Pay",
        "google_pay" => "Google Pay",
        "link" => "Link",
        "sepa_debit" => "SEPA Direct Debit",
        "cash" => "Cash",
        "bank-transfer" => "Bank transfer",
        "card-terminal" => "Card terminal",
        var m => char.ToUpperInvariant(m[0]) + m[1..],
    };

    private static string HumaniseStatus(BookingStatus status, Func<string, string?> get) =>
        get($"document.status.{status.ToString().ToLowerInvariant()}") ?? status switch
        {
            BookingStatus.Confirmed => "Paid",
            BookingStatus.CheckedIn => "Paid",
            BookingStatus.Completed => "Paid",
            BookingStatus.PartiallyRefunded => "Partially refunded",
            BookingStatus.Refunded => "Refunded",
            _ => status.ToString(),
        };

    private static string CancellationPolicyText(
        Func<string, string?> get, Domain.Pricing.CancellationPolicy? policy, CultureInfo culture)
    {
        var seeded = get("document.cancellation_policy");
        if (!string.IsNullOrWhiteSpace(seeded))
        {
            return seeded!;
        }

        if (policy is null || policy.Tiers.Count == 0)
        {
            return "Free cancellation up to 30 days before check-in; see gaiaskyline.com for the full policy.";
        }

        var parts = policy.Tiers
            .OrderByDescending(t => t.DaysBeforeCheckIn)
            .Select(t => t.DaysBeforeCheckIn == 0
                ? $"within {policy.Tiers.Min(x => x.DaysBeforeCheckIn == 0 ? int.MaxValue : x.DaysBeforeCheckIn)} days: {t.RefundPct}% refund"
                : $"{t.DaysBeforeCheckIn}+ days before check-in: {t.RefundPct}% refund");
        return string.Join("; ", parts) + ".";
    }

    private static CultureInfo CultureFor(string language)
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

    private static string Normalize(string language)
    {
        var lang = language?.Trim();
        return string.IsNullOrEmpty(lang) ? "en" : lang;
    }
}
