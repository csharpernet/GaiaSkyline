using System.Globalization;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Documents;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// The branded PDF payout statement a partner receives each month (Stage 8 Part A, ADR 0021): one line per
/// commissioned booking with its basis, percentage and amount. Shares the <see cref="DocumentTheme"/> brand
/// language with the guest documents — the Fraunces wordmark, the embedded fonts and the site palette — so
/// everything Gaia Skyline issues looks of a piece (Community licence, ADR 0012).
/// </summary>
internal sealed class PartnerStatementPdfService(AppDbContext dbContext) : IPartnerStatementPdfService
{
    private static readonly string[] TableHeaders = ["Booking", "Stay", "Basis", "%", "Commission"];

    public async Task<byte[]?> GenerateAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var id = PayoutId.From(payoutId);
        var payout = await dbContext.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payout is null)
        {
            return null;
        }

        var payoutPartnerId = payout.PartnerId;
        var partner = await dbContext.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == payoutPartnerId, cancellationToken);
        if (partner is null)
        {
            return null;
        }

        var lines = await (
            from c in dbContext.Commissions
            join b in dbContext.Bookings on c.BookingId equals b.Id
            where c.PayoutId == id
            orderby b.CheckIn
            select new { b.ReferenceCode, b.CheckIn, b.CheckOut, c.BasisAmount, c.Pct, c.Amount })
            .ToListAsync(cancellationToken);

        DocumentTheme.EnsureInitialised();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(44);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x =>
                    x.FontFamily(DocumentTheme.Body).FontSize(10).FontColor(DocumentTheme.Ink).LineHeight(1.35f));

                page.Header().Column(header =>
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
                            meta.Item().Text("Partner payout statement")
                                .FontFamily(DocumentTheme.Heading).FontSize(15).SemiBold().FontColor(DocumentTheme.Ink);
                            meta.Item().PaddingTop(4).Text(payout.PeriodLabel).SemiBold();
                        });
                    });

                    header.Item().PaddingTop(14).LineHorizontal(1).LineColor(DocumentTheme.Fog);
                });

                page.Content().PaddingTop(24).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Column(who =>
                    {
                        who.Spacing(3);
                        who.Item().Text(partner.Name).SemiBold();
                        who.Item().Text(t =>
                        {
                            t.Span("Email: ").FontColor(DocumentTheme.River);
                            t.Span(partner.Email);
                        });
                        if (partner.PayoutIban is { } iban && iban.Length >= 4)
                        {
                            who.Item().Text(t =>
                            {
                                t.Span("Paid to IBAN ending ").FontColor(DocumentTheme.River);
                                t.Span($"{iban[^4..]} · {partner.PayoutAccountHolder}");
                            });
                        }
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); // booking
                            c.RelativeColumn(3); // stay
                            c.RelativeColumn(2); // basis
                            c.RelativeColumn(1); // pct
                            c.RelativeColumn(2); // commission
                        });

                        table.Header(h =>
                        {
                            foreach (var title in TableHeaders)
                            {
                                h.Cell().BorderBottom(1).BorderColor(DocumentTheme.Fog).PaddingVertical(4)
                                    .Text(title).SemiBold().FontSize(9).FontColor(DocumentTheme.River);
                            }
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().PaddingVertical(3).Text(line.ReferenceCode);
                            table.Cell().PaddingVertical(3).Text(
                                $"{line.CheckIn.ToString("d MMM", CultureInfo.InvariantCulture)} – " +
                                $"{line.CheckOut.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
                            table.Cell().PaddingVertical(3).AlignRight().Text(Euro(line.BasisAmount.Amount));
                            table.Cell().PaddingVertical(3).AlignRight().Text($"{line.Pct}%");
                            table.Cell().PaddingVertical(3).AlignRight().Text(Euro(line.Amount.Amount));
                        }

                        table.Cell().ColumnSpan(4).BorderTop(1).BorderColor(DocumentTheme.Fog).PaddingTop(6)
                            .Text("Total payout").SemiBold();
                        table.Cell().BorderTop(1).BorderColor(DocumentTheme.Fog).PaddingTop(6).AlignRight()
                            .Text(Euro(payout.Amount.Amount)).SemiBold();
                    });

                    col.Item().PaddingTop(4).Text(
                            "The commission basis is the booking total excluding tourist tax and the cleaning fee, " +
                            "net of refunds (ADR 0020). The transfer is made manually to the IBAN on file.")
                        .FontSize(8).FontColor(DocumentTheme.River);
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().PaddingTop(8).LineHorizontal(1).LineColor(DocumentTheme.Fog);
                    footer.Item().PaddingTop(6).Text("Gaia Skyline · Vila Nova de Gaia · AL 175890/AL")
                        .FontSize(8).FontColor(DocumentTheme.River);
                    footer.Item().Text("Partner program payout statement — not a tax document.")
                        .FontSize(8).Italic().FontColor(DocumentTheme.River);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string Euro(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture) + " €";
}
