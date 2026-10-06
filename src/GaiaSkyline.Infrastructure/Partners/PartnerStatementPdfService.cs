using System.Globalization;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// The branded PDF payout statement a partner receives each month (Stage 8 Part A, ADR 0021): one line per
/// commissioned booking with its basis, percentage and amount. Same QuestPDF styling as the invoice
/// (Community licence, ADR 0012).
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

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#0F1417"));

                page.Header().Column(header =>
                {
                    header.Item().Text("Gaia Skyline").FontSize(22).Bold().FontColor("#A04A28");
                    header.Item().Text("Partner program — payout statement").FontSize(9).FontColor("#2E4F60");
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Text($"Payout statement — {payout.PeriodLabel}").FontSize(14).SemiBold();
                    col.Item().Text($"Partner: {partner.Name} ({partner.Email})");
                    if (partner.PayoutIban is { } iban && iban.Length >= 4)
                    {
                        col.Item().Text($"Paid to IBAN ending {iban[^4..]} · {partner.PayoutAccountHolder}");
                    }

                    col.Item().PaddingTop(10).Table(table =>
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
                                h.Cell().BorderBottom(1).PaddingVertical(3).Text(title).SemiBold().FontSize(9);
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

                        table.Cell().ColumnSpan(4).BorderTop(1).PaddingTop(6).Text("Total payout").Bold();
                        table.Cell().BorderTop(1).PaddingTop(6).AlignRight().Text(Euro(payout.Amount.Amount)).Bold();
                    });

                    col.Item().PaddingTop(8).Text(
                            "The commission basis is the booking total excluding tourist tax and the cleaning fee, " +
                            "net of refunds (ADR 0020). The transfer is made manually to the IBAN on file.")
                        .FontSize(8).FontColor("#2E4F60");
                });

                page.Footer().AlignCenter().Text("Gaia Skyline · Vila Nova de Gaia · partner program")
                    .FontSize(9).FontColor("#2E4F60");
            });
        });

        return document.GeneratePdf();
    }

    private static string Euro(decimal amount) => "€" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
