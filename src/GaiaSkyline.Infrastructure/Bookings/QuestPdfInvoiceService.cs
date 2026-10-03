using System.Globalization;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>Builds the branded PDF invoice with QuestPDF (Community licence; see ADR 0012).</summary>
internal sealed class QuestPdfInvoiceService(IBookingReadStore bookingReadStore) : IInvoiceService
{
    private static readonly BookingStatus[] Payable =
    [
        BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed,
        BookingStatus.Refunded, BookingStatus.PartiallyRefunded,
    ];

    public async Task<byte[]?> GenerateAsync(string referenceCode, CancellationToken cancellationToken)
    {
        var booking = await bookingReadStore.GetByReferenceAsync(referenceCode, cancellationToken);
        if (booking is null || !Payable.Contains(booking.Status))
        {
            return null;
        }

        return BuildPdf(booking);
    }

    private static byte[] BuildPdf(BookingSummaryDto b)
    {
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
                    header.Item().Text("Gaia Skyline Apartment · Vila Nova de Gaia · AL 175890/AL")
                        .FontSize(9).FontColor("#2E4F60");
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Text($"Invoice — booking {b.ReferenceCode}").FontSize(14).SemiBold();

                    col.Item().Text($"Guest: {b.GuestName}");
                    col.Item().Text(
                        $"Stay: {b.CheckIn.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – " +
                        $"{b.CheckOut.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} ({b.Nights} nights)");
                    col.Item().Text($"Guests: {b.Adults} adults, {b.Children} children, {b.Infants} infants");

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });

                        void Row(string label, decimal amount, bool negative = false)
                        {
                            table.Cell().PaddingVertical(3).Text(label);
                            table.Cell().PaddingVertical(3).AlignRight()
                                .Text((negative ? "−" : string.Empty) + Euro(amount));
                        }

                        Row("Accommodation", b.Subtotal);
                        if (b.DiscountAmount > 0)
                        {
                            Row("Discount", b.DiscountAmount, negative: true);
                        }

                        if (b.CleaningFee > 0)
                        {
                            Row("Cleaning fee", b.CleaningFee);
                        }

                        if (b.TouristTax > 0)
                        {
                            Row("Tourist tax", b.TouristTax);
                        }

                        table.Cell().BorderTop(1).PaddingTop(6).Text("Total").Bold();
                        table.Cell().BorderTop(1).PaddingTop(6).AlignRight().Text(Euro(b.Total)).Bold();
                    });
                });

                page.Footer().AlignCenter().Text("Thank you for booking direct with Gaia Skyline.")
                    .FontSize(9).FontColor("#2E4F60");
            });
        });

        return document.GeneratePdf();
    }

    private static string Euro(decimal amount) => "€" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
