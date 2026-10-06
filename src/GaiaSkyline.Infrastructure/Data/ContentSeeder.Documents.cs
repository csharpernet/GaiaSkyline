using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Data;

/// <summary>
/// Seeds the <c>document.*</c> content blocks — the labels and fixed phrases on the branded guest PDFs
/// (Stage 8) — with real translations in all five languages, so a confirmation/receipt reads correctly in
/// the guest's language. Owner-editable in the admin like every other content block. The service still
/// carries English defaults, so a document renders even if a key is cleared.
/// </summary>
public sealed partial class ContentSeeder
{
    private sealed record DocText(string Key, string En, string Pt, string Es, string Fr, string De);

    private static readonly DocText[] DocumentBlocks =
    [
        new("document.confirmation.title", "Booking confirmation & receipt", "Confirmação de reserva e recibo", "Confirmación de reserva y recibo", "Confirmation de réservation et reçu", "Buchungsbestätigung & Beleg"),
        new("document.receipt.title", "Payment receipt", "Recibo de pagamento", "Recibo de pago", "Reçu de paiement", "Zahlungsbeleg"),
        new("document.cancellation.title", "Cancellation & refund receipt", "Recibo de cancelamento e reembolso", "Recibo de cancelación y reembolso", "Reçu d'annulation et de remboursement", "Stornierungs- & Erstattungsbeleg"),

        new("document.label.issued", "Issued", "Emitido", "Emitido", "Émis le", "Ausgestellt"),
        new("document.label.stay", "Your stay", "A sua estadia", "Su estancia", "Votre séjour", "Ihr Aufenthalt"),
        new("document.label.checkin", "Check-in", "Check-in", "Entrada", "Arrivée", "Anreise"),
        new("document.label.checkout", "Check-out", "Check-out", "Salida", "Départ", "Abreise"),
        new("document.label.checkin_time", "from 16:00", "a partir das 16:00", "desde las 16:00", "à partir de 16h00", "ab 16:00 Uhr"),
        new("document.label.checkout_time", "until 10:00", "até às 10:00", "hasta las 10:00", "jusqu'à 10h00", "bis 10:00 Uhr"),
        new("document.label.nights", "Nights", "Noites", "Noches", "Nuits", "Nächte"),
        new("document.label.guests", "Guests", "Hóspedes", "Huéspedes", "Voyageurs", "Gäste"),
        new("document.label.guest_details", "Guest details", "Dados do hóspede", "Datos del huésped", "Coordonnées du voyageur", "Gästedaten"),
        new("document.label.email", "Email", "Email", "Correo electrónico", "E-mail", "E-Mail"),
        new("document.label.phone", "Phone", "Telefone", "Teléfono", "Téléphone", "Telefon"),
        new("document.label.country", "Country", "País", "País", "Pays", "Land"),
        new("document.label.price", "Price breakdown", "Detalhe de preços", "Desglose de precios", "Détail des prix", "Preisaufschlüsselung"),
        new("document.label.accommodation", "Accommodation", "Alojamento", "Alojamiento", "Hébergement", "Unterkunft"),
        new("document.label.discount", "Discount", "Desconto", "Descuento", "Remise", "Rabatt"),
        new("document.label.cleaning", "Cleaning fee", "Taxa de limpeza", "Tarifa de limpieza", "Frais de ménage", "Reinigungsgebühr"),
        new("document.label.tourist_tax", "Tourist tax", "Taxa turística", "Impuesto turístico", "Taxe de séjour", "Kurtaxe"),
        new("document.label.total", "Total", "Total", "Total", "Total", "Gesamt"),
        new("document.label.payment", "Payment", "Pagamento", "Pago", "Paiement", "Zahlung"),
        new("document.label.method", "Method", "Método", "Método", "Moyen", "Zahlart"),
        new("document.label.paid_on", "Paid on", "Pago em", "Pagado el", "Payé le", "Bezahlt am"),
        new("document.label.amount", "Amount", "Valor", "Importe", "Montant", "Betrag"),
        new("document.label.status", "Status", "Estado", "Estado", "Statut", "Status"),
        new("document.label.refund", "Refund", "Reembolso", "Reembolso", "Remboursement", "Erstattung"),
        new("document.label.refunded", "Amount refunded", "Valor reembolsado", "Importe reembolsado", "Montant remboursé", "Erstatteter Betrag"),
        new("document.label.refund_date", "Refunded on", "Reembolsado em", "Reembolsado el", "Remboursé le", "Erstattet am"),
        new("document.label.reason", "Reason", "Motivo", "Motivo", "Motif", "Grund"),
        new("document.label.cancellation_policy", "Cancellation policy", "Política de cancelamento", "Política de cancelación", "Politique d'annulation", "Stornierungsbedingungen"),
        new("document.label.arrival", "Before you arrive", "Antes de chegar", "Antes de llegar", "Avant votre arrivée", "Vor Ihrer Anreise"),
        new("document.label.adults", "adults", "adultos", "adultos", "adultes", "Erwachsene"),
        new("document.label.children", "children", "crianças", "niños", "enfants", "Kinder"),
        new("document.label.infants", "infants", "bebés", "bebés", "bébés", "Kleinkinder"),

        new("document.status.confirmed", "Paid", "Pago", "Pagado", "Payé", "Bezahlt"),
        new("document.status.checkedin", "Paid", "Pago", "Pagado", "Payé", "Bezahlt"),
        new("document.status.completed", "Paid", "Pago", "Pagado", "Payé", "Bezahlt"),
        new("document.status.partiallyrefunded", "Partially refunded", "Parcialmente reembolsado", "Parcialmente reembolsado", "Partiellement remboursé", "Teilweise erstattet"),
        new("document.status.refunded", "Refunded", "Reembolsado", "Reembolsado", "Remboursé", "Erstattet"),

        new("document.arrival.checkin", "Check-in is from 16:00 with a smart lock; your personal entry code arrives by email on the morning of arrival.", "O check-in é a partir das 16:00 com fechadura inteligente; o seu código de entrada é enviado por email na manhã da chegada.", "La entrada es a partir de las 16:00 con cerradura inteligente; su código de acceso llega por correo la mañana de la llegada.", "L'arrivée se fait à partir de 16h00 avec une serrure connectée ; votre code d'accès arrive par e-mail le matin de votre arrivée.", "Check-in ab 16:00 Uhr mit Smart-Lock; Ihr persönlicher Zugangscode kommt am Morgen der Anreise per E-Mail."),
        new("document.arrival.late_fee", "Arriving after 23:00? A €30 cash late check-in fee applies.", "Chega depois das 23:00? Aplica-se uma taxa de check-in tardio de 30 € em dinheiro.", "¿Llega después de las 23:00? Se aplica una tarifa de entrada tardía de 30 € en efectivo.", "Arrivée après 23h00 ? Des frais d'arrivée tardive de 30 € en espèces s'appliquent.", "Ankunft nach 23:00 Uhr? Es fällt eine Gebühr für späten Check-in von 30 € in bar an."),
        new("document.arrival.contact", "Questions? Message your host any time — we usually reply within the hour.", "Dúvidas? Contacte o seu anfitrião a qualquer momento — respondemos geralmente numa hora.", "¿Preguntas? Escriba a su anfitrión en cualquier momento; solemos responder en una hora.", "Des questions ? Écrivez à votre hôte à tout moment — nous répondons généralement dans l'heure.", "Fragen? Schreiben Sie Ihrem Gastgeber jederzeit — wir antworten meist innerhalb einer Stunde."),
        new("document.cancellation_policy", "Free cancellation up to 30 days before check-in. See gaiaskyline.com for the full policy.", "Cancelamento gratuito até 30 dias antes do check-in. Consulte gaiaskyline.com para a política completa.", "Cancelación gratuita hasta 30 días antes de la entrada. Consulte gaiaskyline.com para la política completa.", "Annulation gratuite jusqu'à 30 jours avant l'arrivée. Voir gaiaskyline.com pour la politique complète.", "Kostenlose Stornierung bis 30 Tage vor Anreise. Die vollständigen Bedingungen finden Sie auf gaiaskyline.com."),
        new("document.qr.caption", "Scan to manage your booking", "Digitalize para gerir a sua reserva", "Escanee para gestionar su reserva", "Scannez pour gérer votre réservation", "Scannen, um Ihre Buchung zu verwalten"),
        new("document.footer.not_tax_invoice", "This document is a booking confirmation/receipt and is not a tax invoice (fatura).", "Este documento é uma confirmação/recibo de reserva e não é uma fatura.", "Este documento es una confirmación/recibo de reserva y no es una factura.", "Ce document est une confirmation/un reçu de réservation et n'est pas une facture.", "Dieses Dokument ist eine Buchungsbestätigung/ein Beleg und keine Rechnung."),
        new("document.footer.contact", "stay@gaiaskyline.com · Vila Nova de Gaia, Portugal", "stay@gaiaskyline.com · Vila Nova de Gaia, Portugal", "stay@gaiaskyline.com · Vila Nova de Gaia, Portugal", "stay@gaiaskyline.com · Vila Nova de Gaia, Portugal", "stay@gaiaskyline.com · Vila Nova de Gaia, Portugal"),
    ];

    private async Task EnsureDocumentBlocksAsync(CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.ContentBlocks.Select(b => b.Key).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < DocumentBlocks.Length; i++)
        {
            var spec = DocumentBlocks[i];
            if (existing.Contains(spec.Key))
            {
                continue;
            }

            var block = new ContentBlock(
                ContentBlockId.From(DeterministicGuid.From($"block:{spec.Key}")),
                spec.Key,
                ContentKind.ShortText,
                "document",
                spec.Key["document.".Length..],
                displayOrder: 900 + i,
                isPublished: true,
                SeedTimestampUtc,
                Actor);

            block.SetTranslation("en", spec.En, null, null, null, SeedTimestampUtc, Actor);
            block.SetTranslation("pt-PT", spec.Pt, null, null, null, SeedTimestampUtc, Actor);
            block.SetTranslation("es", spec.Es, null, null, null, SeedTimestampUtc, Actor);
            block.SetTranslation("fr", spec.Fr, null, null, null, SeedTimestampUtc, Actor);
            block.SetTranslation("de", spec.De, null, null, null, SeedTimestampUtc, Actor);

            _dbContext.ContentBlocks.Add(block);
        }
    }
}
