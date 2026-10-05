using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>
/// Polls each enabled external calendar: fetches the feed (with HTTP retry/circuit-breaker/timeout),
/// short-circuits when the SHA-256 is unchanged, parses tolerantly, filters out our own exported events,
/// upserts blocks by (source, UID), soft-deletes vanished ones, records sync state, and flags conflicts
/// with active bookings. Zero enabled sources = manual mode (logged once, no failure).
/// </summary>
internal sealed class ExternalCalendarImporter(
    AppDbContext dbContext,
    HttpClient httpClient,
    ExternalCalendarUrlProtector urlProtector,
    IAvailabilityService availabilityService,
    IIcsCacheInvalidator icsCacheInvalidator,
    IEmailSender emailSender,
    IOptionsSnapshot<EmailOptions> emailOptions,
    TimeProvider clock,
    ILogger<ExternalCalendarImporter> logger) : IExternalCalendarImporter
{
    private static readonly BookingStatus[] ConflictStatuses = [BookingStatus.Confirmed, BookingStatus.CheckedIn];

    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Constant,
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.9,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            BreakDuration = TimeSpan.FromSeconds(30),
        })
        .AddTimeout(TimeSpan.FromSeconds(30))
        .Build();

    public async Task ImportAllAsync(CancellationToken cancellationToken)
    {
        var sources = await dbContext.ExternalCalendarSources
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        if (sources.Count == 0)
        {
            logger.LogInformation("No external calendars configured — manual mode.");
            return;
        }

        var changed = false;
        foreach (var source in sources)
        {
            changed |= await ImportSourceAsync(source, cancellationToken);
        }

        if (changed)
        {
            availabilityService.Invalidate();
            icsCacheInvalidator.Invalidate();
        }
    }

    private async Task<bool> ImportSourceAsync(ExternalCalendarSource source, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        string body;
        try
        {
            var url = urlProtector.Unprotect(source.IcsUrlProtected);
            body = await Pipeline.ExecuteAsync(
                async token =>
                {
                    using var response = await httpClient.GetAsync(new Uri(url), token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(token);
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or CryptographicException or BrokenCircuitException or TimeoutRejectedException)
        {
            source.RecordFailure(ex.Message, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "External calendar sync failed for source {Source}.", source.Name);
            return false;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        if (string.Equals(hash, source.LastHash, StringComparison.Ordinal))
        {
            source.RecordUnchanged(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        // Skip events that originated from our own export (they'd otherwise bounce back in).
        var events = IcsParser.Parse(body).Where(e => !IsOurEcho(e.Uid)).ToList();

        var existing = await dbContext.ExternalCalendarBlocks
            .Where(b => b.SourceId == source.Id)
            .ToListAsync(cancellationToken);
        var byUid = existing
            .Where(b => b.ExternalUid is not null)
            .GroupBy(b => b.ExternalUid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in events)
        {
            seen.Add(e.Uid);
            if (byUid.TryGetValue(e.Uid, out var block))
            {
                block.Refresh(e.Start, e.EndInclusive, e.Summary, now);
            }
            else
            {
                dbContext.ExternalCalendarBlocks.Add(new ExternalCalendarBlock(
                    ExternalCalendarBlockId.New(), source.Name, e.Start, e.EndInclusive, now, e.Uid, e.Summary, source.Id));
            }
        }

        foreach (var block in existing.Where(b => b.IsActive && b.ExternalUid is not null && !seen.Contains(b.ExternalUid!)))
        {
            block.Deactivate();
        }

        source.RecordSuccess(hash, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        await DetectConflictsAsync(source, now, cancellationToken);
        return true;
    }

    private async Task DetectConflictsAsync(ExternalCalendarSource source, DateTime now, CancellationToken cancellationToken)
    {
        var blocks = await dbContext.ExternalCalendarBlocks.AsNoTracking()
            .Where(b => b.IsActive && b.SourceId == source.Id)
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return;
        }

        var bookings = await dbContext.Bookings.AsNoTracking()
            .Where(b => ConflictStatuses.Contains(b.Status))
            .Select(b => new { b.ReferenceCode, b.CheckIn, b.CheckOut })
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            foreach (var block in blocks)
            {
                // Block end is inclusive; booking checkout is exclusive.
                if (block.StartDate < booking.CheckOut && booking.CheckIn <= block.EndDate)
                {
                    await RecordConflictAsync(booking.ReferenceCode, source.Name, block.StartDate, block.EndDate, now, cancellationToken);
                }
            }
        }
    }

    private async Task RecordConflictAsync(
        string reference, string sourceName, DateOnly start, DateOnly end, DateTime now, CancellationToken cancellationToken)
    {
        var normalizedRef = reference.Trim().ToUpperInvariant();
        var exists = await dbContext.BookingConflicts
            .AnyAsync(c => c.BookingReference == normalizedRef && c.SourceName == sourceName
                && c.StartDate == start && c.EndDate == end, cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.BookingConflicts.Add(new BookingConflict(BookingConflictId.New(), normalizedRef, sourceName, start, end, now));
        await dbContext.SaveChangesAsync(cancellationToken);

        var startText = start.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var endText = end.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var body =
            $"<p><strong>Calendar conflict.</strong></p><p>An imported block from {sourceName} ({startText} – {endText}) " +
            $"overlaps active direct booking {normalizedRef}. Nothing has been cancelled — resolve it manually.</p>";
        await emailSender.SendAsync(
            new EmailMessage(emailOptions.Value.OwnerAddress, emailOptions.Value.FromName,
                $"Calendar conflict on booking {normalizedRef}", body),
            cancellationToken);
    }

    // Our export UIDs end in "@gaiaskyline" (booking-* / ownerblock-*); never re-import those.
    private static bool IsOurEcho(string uid) =>
        uid.EndsWith("@gaiaskyline", StringComparison.OrdinalIgnoreCase);
}
