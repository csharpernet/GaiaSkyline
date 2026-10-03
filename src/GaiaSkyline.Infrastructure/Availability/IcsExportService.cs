using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>
/// Builds and caches the exported .ics. Cached by the <see cref="IcsCacheInvalidator"/> version (with a
/// 10-minute safety TTL); the ETag is a content hash so unchanged feeds return 304 across restarts.
/// </summary>
internal sealed class IcsExportService(
    AppDbContext dbContext,
    IMemoryCache cache,
    IcsCacheInvalidator invalidator,
    TimeProvider clock) : IIcsExportService
{
    private static readonly BookingStatus[] ExportedStatuses =
    [
        BookingStatus.AwaitingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed,
    ];

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<IcsExportResult> GetAsync(CancellationToken cancellationToken)
    {
        var key = $"ics-export:{invalidator.Version}";
        if (cache.TryGetValue(key, out IcsExportResult? cached) && cached is not null)
        {
            return cached;
        }

        var cutoff = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-30);

        var bookings = await dbContext.Bookings.AsNoTracking()
            .Where(b => ExportedStatuses.Contains(b.Status) && b.CheckOut >= cutoff)
            .Select(b => new { b.ReferenceCode, b.CheckIn, b.CheckOut })
            .ToListAsync(cancellationToken);

        // Only OwnerUnavailable blocks are exported; ExternalBooking blocks already exist in the
        // management system and would echo back once iCal sync is connected.
        var ownerBlocks = await dbContext.OwnerBlocks.AsNoTracking()
            .Where(o => o.Kind == OwnerBlockKind.OwnerUnavailable && o.EndDate >= cutoff)
            .Select(o => new { o.Id, o.StartDate, o.EndDate })
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//GaiaSkyline//export//EN\r\nCALSCALE:GREGORIAN\r\nMETHOD:PUBLISH\r\n");
        foreach (var b in bookings)
        {
            AppendEvent(builder, $"booking-{b.ReferenceCode}@gaiaskyline", b.CheckIn, b.CheckOut, "Booked");
        }

        foreach (var o in ownerBlocks)
        {
            AppendEvent(builder, $"ownerblock-{o.Id.Value:N}@gaiaskyline", o.StartDate, o.EndDate, "Unavailable");
        }

        builder.Append("END:VCALENDAR\r\n");

        var content = Encoding.UTF8.GetBytes(builder.ToString());
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(content))[..16]}\"";
        var result = new IcsExportResult(content, etag);
        cache.Set(key, result, CacheTtl);
        return result;
    }

    private static void AppendEvent(StringBuilder builder, string uid, DateOnly start, DateOnly endExclusive, string summary)
    {
        // All-day events: DTEND is exclusive, matching checkout and the owner block's exclusive end.
        builder.Append("BEGIN:VEVENT\r\nUID:").Append(uid).Append("\r\n")
            .Append("DTSTART;VALUE=DATE:").Append(start.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("DTEND;VALUE=DATE:").Append(endExclusive.ToString("yyyyMMdd", CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("SUMMARY:").Append(summary).Append("\r\n")
            .Append("END:VEVENT\r\n");
    }
}
