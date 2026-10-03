using System.Threading;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>Process-wide cache version for availability, bumped on any booking state change.</summary>
public sealed class AvailabilityCacheState
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Bump() => Interlocked.Increment(ref _version);
}

/// <summary>
/// EF Core implementation of <see cref="IAvailabilityService"/>. Blocked nights = occupancy rows of
/// bookings that are awaiting payment / confirmed / checked-in, unioned with active external-calendar
/// blocks. Cached per month for 60 seconds; <see cref="Invalidate"/> bumps a version so stale months
/// are ignored.
/// </summary>
internal sealed class AvailabilityService(
    AppDbContext dbContext,
    IMemoryCache cache,
    AvailabilityCacheState cacheState) : IAvailabilityService
{
    private static readonly BookingStatus[] BlockingStatuses =
        [BookingStatus.AwaitingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DateOnly>> GetBlockedDatesAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to <= from)
        {
            return [];
        }

        var blocked = new SortedSet<DateOnly>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month < to; month = month.AddMonths(1))
        {
            foreach (var date in await GetMonthBlockedAsync(month.Year, month.Month, cancellationToken))
            {
                if (date >= from && date < to)
                {
                    blocked.Add(date);
                }
            }
        }

        return blocked.ToList();
    }

    public void Invalidate() => cacheState.Bump();

    private async Task<IReadOnlySet<DateOnly>> GetMonthBlockedAsync(int year, int month, CancellationToken cancellationToken)
    {
        var key = $"avail:{cacheState.Version}:{year:D4}-{month:D2}";
        if (cache.TryGetValue(key, out IReadOnlySet<DateOnly>? cached) && cached is not null)
        {
            return cached;
        }

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var occupied = await (
            from o in dbContext.BookingDateOccupancies.AsNoTracking()
            join b in dbContext.Bookings.AsNoTracking() on o.BookingId equals b.Id
            where BlockingStatuses.Contains(b.Status) && o.Date >= monthStart && o.Date <= monthEnd
            select o.Date).ToListAsync(cancellationToken);

        var externalBlocks = await dbContext.ExternalCalendarBlocks.AsNoTracking()
            .Where(x => x.IsActive && x.StartDate <= monthEnd && x.EndDate >= monthStart)
            .Select(x => new { x.StartDate, x.EndDate })
            .ToListAsync(cancellationToken);

        var set = new HashSet<DateOnly>(occupied);
        foreach (var block in externalBlocks)
        {
            var start = block.StartDate < monthStart ? monthStart : block.StartDate;
            var end = block.EndDate > monthEnd ? monthEnd : block.EndDate;
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                set.Add(d);
            }
        }

        IReadOnlySet<DateOnly> result = set;
        cache.Set(key, result, CacheDuration);
        return result;
    }
}
