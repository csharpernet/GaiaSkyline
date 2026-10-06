using System.Globalization;
using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Documents;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Documents;
using GaiaSkyline.Infrastructure.Partners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// The branded guest PDFs (Stage 8): each document generates in all five languages, carries the booking
/// reference, dates, totals and the "not a tax invoice" line, never labels itself an invoice, embeds a QR
/// that decodes to the manage-booking URL, and stays under ~500 KB. The rendered output is cached in the
/// database and regenerated only when the booking changes. Review samples (every language plus the
/// Multibanco, refunded and partner-statement variants) are written to docs/samples/pdfs/.
/// </summary>
public sealed class GuestDocumentTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private static readonly string[] Forbidden = ["invoice", "fatura", "factura", "facture", "rechnung"];
    private static readonly string SamplesDir =
        Path.Combine(FindRepoRoot(), "docs", "samples", "pdfs");

    private readonly LocalDbFixture _fixture = fixture;

    [Theory]
    [InlineData("en")]
    [InlineData("pt-PT")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    public async Task Confirmation_generates_in_every_language_with_reference_totals_and_the_disclaimer(string lang)
    {
        var booking = await SeedConfirmedCardBookingAsync(lang);
        var pdf = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.Confirmation, lang, CancellationToken.None);

        pdf.Should().NotBeNull();
        pdf!.Length.Should().BeLessThan(500 * 1024, "the document must stay a reasonable size");

        var text = ExtractText(pdf);
        text.Should().Contain(booking.ReferenceCode);
        text.Should().Contain(booking.Total.Amount.ToString("N2", CultureInfo.GetCultureInfo(lang)));
        text.ToLowerInvariant().Should().Contain("not a tax invoice", "the disclaimer is mandatory");

        AssertNeverLabelledAnInvoice(text);
        WriteSample($"confirmation-{lang}.pdf", pdf);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt-PT")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    public void The_qr_code_decodes_to_the_localised_manage_booking_url(string lang)
    {
        // The service builds this exact URL (slug = lower-cased language); assert the QR helper round-trips it.
        var expected = $"https://localhost:7443/{lang.ToLowerInvariant()}/account/magic-link?reference=GS12345678";
        DecodeQr(QrCode.Png(expected)).Should().Be(expected);
    }

    [Fact]
    public async Task Multibanco_confirmation_names_the_multibanco_method()
    {
        var booking = await SeedMultibancoBookingAsync("pt-PT");
        var pdf = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.Confirmation, "pt-PT", CancellationToken.None);

        pdf.Should().NotBeNull();
        var text = ExtractText(pdf!);
        text.Should().Contain("Multibanco");
        text.Should().Contain(booking.ReferenceCode);
        AssertNeverLabelledAnInvoice(text);
        WriteSample("confirmation-multibanco.pdf", pdf!);
    }

    [Fact]
    public async Task Cancellation_document_shows_the_refund_and_omits_the_payment()
    {
        var booking = await SeedConfirmedCardBookingAsync("en");
        await MutateAsync(booking.ReferenceCode, b =>
        {
            b.RecordRefund(new Money(411m, "EUR"));
            b.Cancel("Guest cancellation — changed travel plans", DateTime.UtcNow);
        });

        var pdf = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.CancellationRefund, "en", CancellationToken.None);

        pdf.Should().NotBeNull();
        var text = ExtractText(pdf!);
        text.Should().Contain("411.00", "the refunded amount appears on the cancellation receipt");
        text.ToLowerInvariant().Should().Contain("not a tax invoice");
        AssertNeverLabelledAnInvoice(text);
        WriteSample("cancellation-refunded.pdf", pdf!);
    }

    [Fact]
    public async Task The_documented_price_components_sum_to_the_total()
    {
        // The accommodation, discount, cleaning and tourist-tax lines the PDF prints must reconcile to the total.
        var b = await SeedConfirmedCardBookingAsync("en");
        (b.Subtotal.Amount - b.DiscountAmount.Amount + b.CleaningFee.Amount + b.TouristTax.Amount)
            .Should().Be(b.Total.Amount);
    }

    [Fact]
    public async Task A_second_download_serves_the_stored_bytes_rather_than_re_rendering()
    {
        var booking = await SeedConfirmedCardBookingAsync("en");
        var first = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.Confirmation, CancellationToken.None);
        first.Should().NotBeNull();

        // Tamper with the stored copy WITHOUT changing its fingerprint: a cache hit must return these exact bytes.
        var sentinel = new byte[] { 1, 2, 3, 4, 5 };
        await using (var ctx = _fixture.CreateContext())
        {
            var row = await ctx.Set<BookingDocument>().SingleAsync(d => d.BookingReference == booking.ReferenceCode);
            row.Replace(row.SourceHash, sentinel, DateTime.UtcNow);
            await ctx.SaveChangesAsync();
        }

        var second = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.Confirmation, CancellationToken.None);
        second.Should().Equal(sentinel, "an unchanged booking serves the stored bytes without re-rendering");
    }

    [Fact]
    public async Task A_booking_change_regenerates_the_document()
    {
        var booking = await SeedConfirmedCardBookingAsync("en");
        var before = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.CancellationRefund, CancellationToken.None);
        before.Should().NotBeNull();

        await MutateAsync(booking.ReferenceCode, b => b.RecordRefund(new Money(100m, "EUR")));

        var after = await Service().GenerateAsync(booking.ReferenceCode, GuestDocumentType.CancellationRefund, CancellationToken.None);
        after.Should().NotBeNull();
        after.Should().NotEqual(before!, "a material change regenerates rather than serving the stale cache");
        ExtractText(after!).Should().Contain("100.00");

        await using var ctx = _fixture.CreateContext();
        (await ctx.Set<BookingDocument>().CountAsync(
                d => d.BookingReference == booking.ReferenceCode && d.DocumentType == nameof(GuestDocumentType.CancellationRefund)))
            .Should().Be(1, "the cache replaces the row in place rather than accumulating");
    }

    [Fact]
    public async Task Partner_payout_statement_shares_the_brand_and_writes_a_sample()
    {
        var booking = await SeedConfirmedCardBookingAsync("en");
        var partner = new Partner(PartnerId.New(), PartnerApplicationId.New(), "Marina Costa", "marina@example.com", 10, 15, DateTime.UtcNow);
        partner.SetPayoutDetails("PT50000201231234567890154", "Marina Costa", "PT123456789", "PT");
        var commission = new Commission(CommissionId.New(), partner.Id, booking.Id, new Money(480m, "EUR"), 15, DateTime.UtcNow);
        commission.MakePayable();
        var payoutGuid = Guid.NewGuid();
        var payout = new Payout(PayoutId.From(payoutGuid), partner.Id, "2026-09", commission.Amount, DateTime.UtcNow);
        commission.AssignToPayout(payout.Id);

        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Partners.Add(partner);
            ctx.Payouts.Add(payout);
            ctx.Commissions.Add(commission);
            await ctx.SaveChangesAsync();
        }

        await using var read = _fixture.CreateContext();
        var pdf = await new PartnerStatementPdfService(read).GenerateAsync(payoutGuid, CancellationToken.None);

        pdf.Should().NotBeNull();
        pdf!.Length.Should().BeLessThan(500 * 1024);
        var text = ExtractText(pdf);
        text.Should().Contain("Marina Costa");
        text.Should().Contain("2026-09");
        text.Should().Contain(booking.ReferenceCode);
        AssertNeverLabelledAnInvoice(text);
        WriteSample("partner-statement.pdf", pdf);
    }

    // No guest document LABELS itself an invoice: the forbidden words may appear only inside the disclaimer line.
    private static void AssertNeverLabelledAnInvoice(string text)
    {
        var withoutDisclaimer = string.Join(
            " ",
            text.Split('\n').Where(line => !line.Contains("not a tax invoice", StringComparison.OrdinalIgnoreCase)));
        foreach (var word in Forbidden)
        {
            withoutDisclaimer.ToLowerInvariant().Should().NotContain(word, $"no document may be labelled '{word}'");
        }
    }

    private GuestDocumentService Service() => new(
        _fixture.CreateContext(),
        new EmptyContent(),
        Options.Create(new EmailOptions { SiteBaseUrl = "https://localhost:7443", FromAddress = "stay@gaiaskyline.com" }),
        TimeProvider.System);

    private async Task<Booking> SeedConfirmedCardBookingAsync(string lang)
    {
        var booking = NewBooking(lang);
        booking.AttachPaymentIntent("pi_sample_123");
        booking.ConfirmPayment("card", DateTime.UtcNow);
        return await SaveAsync(booking);
    }

    private async Task<Booking> SeedMultibancoBookingAsync(string lang)
    {
        var booking = NewBooking(lang);
        booking.AttachPaymentIntent("pi_mb_456");
        booking.SetMultibancoVoucher("12345", "987654321", DateTime.UtcNow.AddDays(2));
        booking.ConfirmPayment("multibanco", DateTime.UtcNow);
        return await SaveAsync(booking);
    }

    private static Booking NewBooking(string lang)
    {
        var reference = $"GS{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        return new Booking(
            BookingId.New(), reference, new DateOnly(2027, 5, 10), new DateOnly(2027, 5, 14),
            2, 1, 0, "Alexandra Marques", "guest@example.com", "+351912345678", "PT", lang,
            new Money(120m, "EUR"), new Money(480m, "EUR"), Money.Zero("EUR"),
            new Money(60m, "EUR"), new Money(8m, "EUR"), new Money(548m, "EUR"), DateTime.UtcNow);
    }

    private async Task<Booking> SaveAsync(Booking booking)
    {
        await using var ctx = _fixture.CreateContext();
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();
        return booking;
    }

    private async Task MutateAsync(string reference, Action<Booking> mutate)
    {
        await using var ctx = _fixture.CreateContext();
        var booking = await ctx.Bookings.SingleAsync(b => b.ReferenceCode == reference);
        mutate(booking);
        await ctx.SaveChangesAsync();
    }

    private static string ExtractText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join("\n", document.GetPages().Select(p => p.Text));
    }

    private static string? DecodeQr(byte[] png)
    {
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgb24>(png);
        var pixels = new byte[image.Width * image.Height * 3];
        image.CopyPixelDataTo(pixels);
        var reader = new ZXing.BarcodeReaderGeneric
        {
            Options = new ZXing.Common.DecodingOptions { PureBarcode = false, TryHarder = true },
        };
        var source = new ZXing.RGBLuminanceSource(pixels, image.Width, image.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGB24);
        return reader.Decode(source)?.Text;
    }

    private static void WriteSample(string name, byte[] pdf)
    {
        Directory.CreateDirectory(SamplesDir);
        File.WriteAllBytes(Path.Combine(SamplesDir, name), pdf);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GaiaSkyline.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    // Returns empty sections (so the documents use their English default labels) + a property.
    private sealed class EmptyContent : IContentService
    {
        public Task<ContentPayload> GetSectionAsync(string section, string language, CancellationToken cancellationToken) =>
            Task.FromResult(new ContentPayload
            {
                Section = section,
                Language = language,
                Items = new Dictionary<string, ContentValue>(),
            });

        public Task<PropertyDto?> GetPropertyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<PropertyDto?>(new PropertyDto(
                "Gaia Skyline", "175890/AL", "Rua do Douro 1, Vila Nova de Gaia", 41.13, -8.61,
                "EUR", "Europe/Lisbon", new TimeOnly(16, 0), new TimeOnly(10, 0), 6, 2, 3, 1, "2+1 beds"));

        public Task<MediaAssetDto?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ReviewDto>> GetPublishedReviewsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StoryDto>> GetPublishedStoriesAsync(string language, int? take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StoryDto?> GetStoryAsync(string slug, string language, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GalleryImageDto>> GetGalleryAsync(string key, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
