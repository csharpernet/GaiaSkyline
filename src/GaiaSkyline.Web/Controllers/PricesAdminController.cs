using System.Globalization;
using System.Text;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Web.Admin;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner prices manager at /admin/prices (Stage 7 §8): a 12-month rates grid (price, min stay,
/// source and lock per night) with range bulk-set/clear/lock, CSV import with the existing dry-run
/// (old vs new preview) plus template/export downloads, a quote preview showing the per-night source,
/// the rate-sync rejection review, and a setup page for seasons, fees, cancellation tiers and promo
/// codes. All writes audited and toasted.
/// </summary>
[Route("admin/prices")]
public sealed class PricesAdminController(
    IPricingAdminReadService read,
    IPricingAdminService pricing,
    IDailyRateService rates,
    IQuoteService quotes,
    IAuditLog audit) : AdminControllerBase
{
    private const int GridMonths = 12;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Prices";
        return View(await BuildIndexAsync(null, null, null, null, new QuotePreviewForm(), cancellationToken));
    }

    [HttpPost("bulk-set")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSet(DateOnly? from, DateOnly? to, string? price, string? minNights, CancellationToken cancellationToken)
    {
        if (from is not { } start || to is not { } end || end < start)
        {
            Toast("Pick a start date and an end date (inclusive).", "error");
            return LocalRedirect("/admin/prices");
        }

        if (!AdminMoney.TryParse(price, out var priceEur))
        {
            Toast("The price is not a valid number.", "error");
            return LocalRedirect("/admin/prices");
        }

        int? min = null;
        if (!string.IsNullOrWhiteSpace(minNights))
        {
            if (!int.TryParse(minNights, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                Toast("Minimum nights must be a whole number of at least 1.", "error");
                return LocalRedirect("/admin/prices");
            }

            min = parsed;
        }

        if (priceEur is not { } p || p <= 0)
        {
            Toast("Enter a nightly price above €0.", "error");
            return LocalRedirect("/admin/prices");
        }

        await rates.SetRangeAsync(start, end, p, min, ActorName, cancellationToken);
        await audit.WriteAsync("pricing.set", ActorId, Ip, "DailyRate", $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}",
            new { price = p, minNights = min }, cancellationToken);
        Toast($"Set €{p:0.##}{(min is null ? "" : $" / min {min} nights")} for {start:yyyy-MM-dd} → {end:yyyy-MM-dd}.");
        return LocalRedirect("/admin/prices");
    }

    [HttpPost("bulk-clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkClear(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        if (from is not { } start || to is not { } end || end < start)
        {
            Toast("Pick a start date and an end date (inclusive).", "error");
            return LocalRedirect("/admin/prices");
        }

        var cleared = await rates.ClearRangeAsync(start, end, ActorName, cancellationToken);
        await audit.WriteAsync("pricing.clear", ActorId, Ip, "DailyRate", $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}",
            new { cleared }, cancellationToken);
        Toast($"Cleared {cleared} per-date override(s); those nights fall back to season/base pricing.");
        return LocalRedirect("/admin/prices");
    }

    [HttpPost("bulk-lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkLock(DateOnly? from, DateOnly? to, bool locked, CancellationToken cancellationToken)
    {
        if (from is not { } start || to is not { } end || end < start)
        {
            Toast("Pick a start date and an end date (inclusive).", "error");
            return LocalRedirect("/admin/prices");
        }

        var count = await rates.SetLockedRangeAsync(start, end, locked, ActorName, cancellationToken);
        await audit.WriteAsync(locked ? "pricing.lock" : "pricing.unlock", ActorId, Ip, "DailyRate",
            $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}", new { count }, cancellationToken);
        Toast(count == 0
            ? "No per-date overrides in that range — set a price first, then lock it."
            : $"{(locked ? "Locked" : "Unlocked")} {count} date(s). Locked dates are never overwritten by imports.");
        return LocalRedirect("/admin/prices");
    }

    [HttpPost("csv/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CsvPreview(IFormFile? csvFile, string? csvText, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Prices";
        var csv = await ReadCsvAsync(csvFile, csvText, cancellationToken);
        if (string.IsNullOrWhiteSpace(csv))
        {
            return View("Index", await BuildIndexAsync(null, null, null, "Upload a CSV file or paste CSV text.", new QuotePreviewForm(), cancellationToken));
        }

        var preview = await read.PreviewCsvAsync(csv, cancellationToken);
        return View("Index", await BuildIndexAsync(preview, csv, null, null, new QuotePreviewForm(), cancellationToken));
    }

    [HttpPost("csv/apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CsvApply(string csv, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            Toast("Nothing to apply — preview a CSV first.", "error");
            return LocalRedirect("/admin/prices");
        }

        var applied = await rates.ApplyCsvAsync(csv, ActorName, cancellationToken);
        await audit.WriteAsync("pricing.csv_import", ActorId, Ip, "DailyRate", null, new { applied }, cancellationToken);
        Toast($"Applied {applied} rate(s) from the CSV.");
        return LocalRedirect("/admin/prices");
    }

    [HttpGet("template.csv")]
    public IActionResult Template()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sb = new StringBuilder("date,price,min_nights\n");
        for (var i = 1; i <= 3; i++)
        {
            sb.Append(today.AddDays(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(",120,3\n");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "gaia-skyline-rates-template.csv");
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var csv = await read.ExportRatesCsvAsync(cancellationToken);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "gaia-skyline-rates.csv");
    }

    [HttpPost("quote-preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuotePreview(QuotePreviewForm form, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Prices";
        if (form.CheckIn is not { } checkIn || form.CheckOut is not { } checkOut || checkOut <= checkIn)
        {
            return View("Index", await BuildIndexAsync(null, null, null, null, form, cancellationToken, "Pick a check-in and a later check-out."));
        }

        try
        {
            var quote = await quotes.QuoteAsync(
                new QuoteRequest(checkIn, checkOut, new GuestParty(form.Adults, form.Children, form.Infants), form.Promo),
                cancellationToken);
            return View("Index", await BuildIndexAsync(null, null, quote, null, form, cancellationToken));
        }
        catch (BelowMinimumNightsException ex)
        {
            return View("Index", await BuildIndexAsync(null, null, null, null, form, cancellationToken,
                $"Below the minimum stay — this check-in needs at least {ex.MinimumNights} night(s)."));
        }
        catch (NoPriceForDateException ex)
        {
            return View("Index", await BuildIndexAsync(null, null, null, null, form, cancellationToken,
                $"No price covers {ex.Date:yyyy-MM-dd} — add a season or a per-date rate."));
        }
    }

    [HttpPost("rejections/{id:guid}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRejection(Guid id, CancellationToken cancellationToken)
    {
        var result = await pricing.AcceptRejectionAsync(id, ActorName, cancellationToken);
        return await FinishAsync(result, "pricing.rejection.accept", "RateSyncRejection", id.ToString(),
            "Accepted as a manual price.", "/admin/prices", cancellationToken);
    }

    [HttpPost("rejections/{id:guid}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissRejection(Guid id, CancellationToken cancellationToken)
    {
        var result = await pricing.DismissRejectionAsync(id, cancellationToken);
        return await FinishAsync(result, "pricing.rejection.dismiss", "RateSyncRejection", id.ToString(),
            "Rejection dismissed.", "/admin/prices", cancellationToken);
    }

    // ---------- Setup: seasons, fees, cancellation tiers, promo codes ----------

    [HttpGet("setup")]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Pricing setup";
        return View(new PricesSetupViewModel(
            await read.GetSeasonsAsync(cancellationToken),
            await read.GetFeesAsync(cancellationToken),
            await read.GetPolicyTiersAsync(cancellationToken),
            await read.GetPromosAsync(cancellationToken)));
    }

    [HttpPost("seasons")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSeason(
        DateOnly? start, DateOnly? end, string? price, int minNights, int weeklyPct, int monthlyPct, CancellationToken cancellationToken)
    {
        if (!TryBuildSeason(start, end, price, minNights, weeklyPct, monthlyPct, out var season, out var error))
        {
            Toast(error!, "error");
            return LocalRedirect("/admin/prices/setup");
        }

        var result = await pricing.CreateSeasonAsync(season!, cancellationToken);
        return await FinishAsync(result, "pricing.season.create", "PricingRule", null, "Season added.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("seasons/{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSeason(
        Guid id, DateOnly? start, DateOnly? end, string? price, int minNights, int weeklyPct, int monthlyPct, CancellationToken cancellationToken)
    {
        if (!TryBuildSeason(start, end, price, minNights, weeklyPct, monthlyPct, out var season, out var error))
        {
            Toast(error!, "error");
            return LocalRedirect("/admin/prices/setup");
        }

        var result = await pricing.UpdateSeasonAsync(id, season!, cancellationToken);
        return await FinishAsync(result, "pricing.season.update", "PricingRule", id.ToString(), "Season updated.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("seasons/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSeason(Guid id, CancellationToken cancellationToken)
    {
        var result = await pricing.DeleteSeasonAsync(id, cancellationToken);
        return await FinishAsync(result, "pricing.season.delete", "PricingRule", id.ToString(), "Season deleted.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("fees")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFees(string? cleaning, string? touristTax, int? touristTaxMaxNights, CancellationToken cancellationToken)
    {
        if (!AdminMoney.TryParse(cleaning, out var cleaningEur) || cleaningEur is null)
        {
            Toast("The cleaning fee is not a valid number.", "error");
            return LocalRedirect("/admin/prices/setup");
        }

        if (!AdminMoney.TryParse(touristTax, out var taxEur))
        {
            Toast("The tourist tax is not a valid number.", "error");
            return LocalRedirect("/admin/prices/setup");
        }

        var result = await pricing.UpdateFeesAsync(new FeesDto(cleaningEur.Value, taxEur, touristTaxMaxNights), cancellationToken);
        return await FinishAsync(result, "pricing.fees.update", "Fee", null, "Fees updated.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("policy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePolicy(int[] days, int[] pct, CancellationToken cancellationToken)
    {
        if (days.Length == 0 || days.Length != pct.Length)
        {
            Toast("Add at least one tier (days + refund %).", "error");
            return LocalRedirect("/admin/prices/setup");
        }

        var tiers = days.Zip(pct, (d, p) => new CancellationTier(d, p)).ToList();
        var result = await pricing.ReplacePolicyTiersAsync(tiers, cancellationToken);
        return await FinishAsync(result, "pricing.policy.update", "CancellationPolicy", null, "Cancellation tiers updated.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("promos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPromo(
        string code, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken)
    {
        var result = await pricing.CreatePromoAsync(code, discountPct, isActive, validFrom, validUntil, cancellationToken);
        return await FinishAsync(result, "pricing.promo.create", "PromoCode", code.Trim().ToUpperInvariant(), "Promo code added.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("promos/{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePromo(
        Guid id, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken)
    {
        var result = await pricing.UpdatePromoAsync(id, discountPct, isActive, validFrom, validUntil, cancellationToken);
        return await FinishAsync(result, "pricing.promo.update", "PromoCode", id.ToString(), "Promo code updated.", "/admin/prices/setup", cancellationToken);
    }

    [HttpPost("promos/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePromo(Guid id, CancellationToken cancellationToken)
    {
        var result = await pricing.DeletePromoAsync(id, cancellationToken);
        return await FinishAsync(result, "pricing.promo.delete", "PromoCode", id.ToString(), "Promo code deleted.", "/admin/prices/setup", cancellationToken);
    }

    // ---------- helpers ----------

    private static bool TryBuildSeason(
        DateOnly? start, DateOnly? end, string? price, int minNights, int weeklyPct, int monthlyPct,
        out SeasonWriteModel? season, out string? error)
    {
        season = null;
        if (start is not { } s || end is not { } e || e < s)
        {
            error = "Pick a season start and end date (inclusive).";
            return false;
        }

        if (!AdminMoney.TryParse(price, out var priceEur) || priceEur is not { } p || p <= 0)
        {
            error = "Enter a nightly price above €0.";
            return false;
        }

        season = new SeasonWriteModel(s, e, p, minNights, weeklyPct, monthlyPct);
        error = null;
        return true;
    }

    private async Task<IActionResult> FinishAsync(
        PricingAdminResult result, string auditEvent, string entityType, string? entityId, string success,
        string backTo, CancellationToken cancellationToken)
    {
        if (!result.Ok)
        {
            Toast(result.Error ?? "The change could not be saved.", "error");
            return LocalRedirect(backTo);
        }

        await audit.WriteAsync(auditEvent, ActorId, Ip, entityType, entityId, null, cancellationToken);
        Toast(success);
        return LocalRedirect(backTo);
    }

    private async Task<PricesAdminViewModel> BuildIndexAsync(
        IReadOnlyList<CsvPreviewRowDto>? csvPreview, string? csvRaw, QuoteBreakdown? quote, string? csvError,
        QuotePreviewForm quoteForm, CancellationToken cancellationToken, string? quoteError = null)
    {
        var firstMonth = DateOnly.FromDateTime(DateTime.UtcNow);
        var grid = await read.GetGridAsync(firstMonth, GridMonths, cancellationToken);
        var rejections = await read.GetOpenRejectionsAsync(cancellationToken);
        return new PricesAdminViewModel(grid, rejections, csvPreview, csvRaw, quote, csvError ?? quoteError, quoteForm);
    }

    private static async Task<string?> ReadCsvAsync(IFormFile? csvFile, string? csvText, CancellationToken cancellationToken)
    {
        if (csvFile is { Length: > 0 and <= 1_000_000 })
        {
            using var reader = new StreamReader(csvFile.OpenReadStream());
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return csvText;
    }
}
