using GaiaSkyline.Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>
/// Lays out a <see cref="GuestDocumentModel"/> as a branded A4 PDF (Stage 8): the GAIA SKYLINE wordmark in
/// Fraunces (the logo), an editorial boutique-hotel feel with generous whitespace, the site palette and the
/// embedded Fraunces/Inter fonts. Pure layout — all values arrive pre-formatted and localised.
/// </summary>
internal sealed class GuestDocumentRenderer(GuestDocumentModel model) : IDocument
{
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(44);
            page.DefaultTextStyle(t => t.FontFamily(DocumentTheme.Body).FontSize(10).FontColor(DocumentTheme.Ink).LineHeight(1.35f));

            page.Header().Element(Header);
            page.Content().PaddingTop(24).Element(Body);
            page.Footer().Element(Footer);
        });
    }

    private void Header(IContainer container)
    {
        container.Column(header =>
        {
            header.Item().Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("GAIA SKYLINE")
                        .FontFamily(DocumentTheme.Heading).FontSize(24).SemiBold().FontColor(DocumentTheme.Clay)
                        .LetterSpacing(0.04f);
                    brand.Item().Text("Vila Nova de Gaia · above the Douro")
                        .FontSize(8).FontColor(DocumentTheme.River);
                });

                row.ConstantItem(220).AlignRight().Column(meta =>
                {
                    meta.Item().Text(model.Title)
                        .FontFamily(DocumentTheme.Heading).FontSize(15).SemiBold().FontColor(DocumentTheme.Ink);
                    meta.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span($"{model.Reference}").SemiBold();
                    });
                    meta.Item().Text($"{model.S.IssuedLabel}: {model.IssueDate}").FontSize(9).FontColor(DocumentTheme.River);
                });
            });

            header.Item().PaddingTop(14).LineHorizontal(1).LineColor(DocumentTheme.Fog);
        });
    }

    private void Body(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(18);

            // Stay summary
            Section(col, model.S.StaySummary, section => section.Column(s =>
            {
                s.Spacing(3);
                s.Item().Text(model.PropertyName).SemiBold();
                s.Item().Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span($"{model.S.CheckInLabel}: ").FontColor(DocumentTheme.River); t.Span($"{model.CheckIn} ({model.S.CheckInTime})"); });
                    r.RelativeItem().Text(t => { t.Span($"{model.S.CheckOutLabel}: ").FontColor(DocumentTheme.River); t.Span($"{model.CheckOut} ({model.S.CheckOutTime})"); });
                });
                s.Item().Text(t => { t.Span($"{model.S.NightsLabel}: ").FontColor(DocumentTheme.River); t.Span(model.Nights); t.Span($"      {model.S.GuestsLabel}: ").FontColor(DocumentTheme.River); t.Span(model.Guests); });
            }));

            // Guest details
            Section(col, model.S.GuestDetails, section => section.Column(s =>
            {
                s.Spacing(3);
                s.Item().Text(model.GuestName).SemiBold();
                s.Item().Text(t => { t.Span($"{model.S.EmailLabel}: ").FontColor(DocumentTheme.River); t.Span(model.GuestEmail); });
                s.Item().Text(t =>
                {
                    t.Span($"{model.S.PhoneLabel}: ").FontColor(DocumentTheme.River);
                    t.Span(model.GuestPhone);
                    t.Span($"      {model.S.CountryLabel}: ").FontColor(DocumentTheme.River);
                    t.Span(model.GuestCountry);
                });
            }));

            // Price breakdown
            Section(col, model.S.PriceBreakdown, section => section.Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(1); });
                foreach (var line in model.PriceLines)
                {
                    table.Cell().PaddingVertical(2).Text(line.Label);
                    table.Cell().PaddingVertical(2).AlignRight().Text((line.Negative ? "−" : string.Empty) + line.Amount);
                }

                table.Cell().BorderTop(1).BorderColor(DocumentTheme.Fog).PaddingTop(6).Text(model.S.TotalLabel).SemiBold();
                table.Cell().BorderTop(1).BorderColor(DocumentTheme.Fog).PaddingTop(6).AlignRight().Text(model.Total).SemiBold();
            }));

            // Payment
            if (model.Payment is { } pay)
            {
                Section(col, model.S.PaymentLabel, section => section.Column(s =>
                {
                    s.Spacing(3);
                    s.Item().Text(t => { t.Span($"{model.S.MethodLabel}: ").FontColor(DocumentTheme.River); t.Span(pay.Method); });
                    s.Item().Text(t => { t.Span($"{model.S.AmountLabel}: ").FontColor(DocumentTheme.River); t.Span(pay.Amount); t.Span($"      {model.S.StatusLabel}: ").FontColor(DocumentTheme.River); t.Span(pay.Status); });
                    s.Item().Text(t => { t.Span($"{model.S.PaidOnLabel}: ").FontColor(DocumentTheme.River); t.Span(pay.PaidOn); });
                    if (!string.IsNullOrEmpty(pay.StripeReference))
                    {
                        s.Item().Text($"Ref: {pay.StripeReference}").FontSize(8).FontColor(DocumentTheme.River);
                    }
                }));
            }

            // Refund
            if (model.Refund is { } refund)
            {
                Section(col, model.S.RefundLabel, section => section.Column(s =>
                {
                    s.Spacing(3);
                    s.Item().Text(t => { t.Span($"{model.S.RefundedLabel}: ").FontColor(DocumentTheme.River); t.Span(refund.Amount).SemiBold(); });
                    s.Item().Text(t => { t.Span($"{model.S.RefundDateLabel}: ").FontColor(DocumentTheme.River); t.Span(refund.Date); });
                    if (!string.IsNullOrEmpty(refund.Reason))
                    {
                        s.Item().Text(t => { t.Span($"{model.S.ReasonLabel}: ").FontColor(DocumentTheme.River); t.Span(refund.Reason); });
                    }
                }));
            }

            // Cancellation policy
            Section(col, model.S.CancellationPolicyLabel, section =>
                section.Text(model.CancellationPolicy).FontSize(9).FontColor(DocumentTheme.Ink));

            // Arrival essentials + QR, side by side
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(s =>
                {
                    s.Item().Text(model.S.ArrivalLabel).FontFamily(DocumentTheme.Heading).FontSize(12).SemiBold().FontColor(DocumentTheme.Clay);
                    s.Item().PaddingTop(4).Text(model.ArrivalCheckIn).FontSize(9);
                    s.Item().PaddingTop(2).Text(model.ArrivalLateFee).FontSize(9);
                    s.Item().PaddingTop(2).Text(model.ArrivalContact).FontSize(9);
                });

                row.ConstantItem(120).AlignRight().Column(qr =>
                {
                    qr.Item().AlignRight().Width(96).Image(model.QrPng);
                    qr.Item().PaddingTop(4).AlignRight().Text(model.QrCaption).FontSize(7.5f).FontColor(DocumentTheme.River);
                });
            });
        });
    }

    private void Footer(IContainer container)
    {
        container.Column(footer =>
        {
            footer.Item().PaddingTop(8).LineHorizontal(1).LineColor(DocumentTheme.Fog);
            footer.Item().PaddingTop(6).Text(model.PropertyRegistration).FontSize(8).FontColor(DocumentTheme.River);
            footer.Item().Text(model.FooterContact).FontSize(8).FontColor(DocumentTheme.River);
            footer.Item().PaddingTop(4).Text(model.FooterNotTaxInvoice).FontSize(8).Italic().FontColor(DocumentTheme.River);
        });
    }

    // A section = a Fraunces heading in clay over its content, as a tidy block.
    private static void Section(ColumnDescriptor col, string heading, Action<IContainer> content)
    {
        col.Item().Column(block =>
        {
            block.Item().Text(heading).FontFamily(DocumentTheme.Heading).FontSize(12).SemiBold().FontColor(DocumentTheme.Clay);
            block.Item().PaddingTop(5).Element(content);
        });
    }
}
