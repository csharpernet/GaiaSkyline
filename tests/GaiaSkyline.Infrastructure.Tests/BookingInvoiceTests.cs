using System.Text;
using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class BookingInvoiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    static BookingInvoiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Invoice_is_a_pdf_for_a_confirmed_booking()
    {
        var reference = await SeedBookingAsync("GS-INV1", confirmed: true);

        await using var context = _fixture.CreateContext();
        var service = new QuestPdfInvoiceService(new BookingReadStore(context));
        var pdf = await service.GenerateAsync(reference, CancellationToken.None);

        pdf.Should().NotBeNull();
        pdf!.Length.Should().BeGreaterThan(500);
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task No_invoice_for_a_booking_still_awaiting_payment()
    {
        var reference = await SeedBookingAsync("GS-INV2", confirmed: false);

        await using var context = _fixture.CreateContext();
        var service = new QuestPdfInvoiceService(new BookingReadStore(context));
        var pdf = await service.GenerateAsync(reference, CancellationToken.None);

        pdf.Should().BeNull();
    }

    private async Task<string> SeedBookingAsync(string reference, bool confirmed)
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), reference, new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 6),
            2, 0, 0, "Invoice Guest", "inv@example.com", "+351 912 345 678", "Portugal", "en",
            nightlyRateSnapshot: new Money(100m, "EUR"), subtotal: new Money(500m, "EUR"),
            discountAmount: new Money(50m, "EUR"), cleaningFee: new Money(60m, "EUR"),
            touristTax: new Money(28m, "EUR"), total: new Money(538m, "EUR"),
            createdAtUtc: DateTime.UtcNow);
        if (confirmed)
        {
            booking.ConfirmPayment("card", DateTime.UtcNow);
        }

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return reference;
    }
}
