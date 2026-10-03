using System.Diagnostics.CodeAnalysis;
using Ical.Net;
using Ical.Net.CalendarComponents;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>A parsed external-calendar event, with an inclusive end date (matching ExternalCalendarBlock).</summary>
public sealed record ParsedIcsEvent(string Uid, DateOnly Start, DateOnly EndInclusive, string? Summary);

/// <summary>
/// Tolerant iCal parser: reads VEVENTs from an .ics feed, handling all-day (exclusive DTEND) and
/// date-time events, varied UID/SUMMARY patterns and missing optional fields. Malformed feeds or
/// individual events are skipped rather than failing the whole import.
/// </summary>
public static class IcsParser
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "A tolerant parser must survive any malformed feed; a bad feed yields no events.")]
    public static IReadOnlyList<ParsedIcsEvent> Parse(string ics)
    {
        var results = new List<ParsedIcsEvent>();
        if (string.IsNullOrWhiteSpace(ics))
        {
            return results;
        }

        Calendar calendar;
        try
        {
            calendar = Calendar.Load(ics);
        }
        catch (Exception)
        {
            return results;
        }

        if (calendar?.Events is null)
        {
            return results;
        }

        foreach (var calendarEvent in calendar.Events)
        {
            var parsed = TryParseEvent(calendarEvent);
            if (parsed is not null)
            {
                results.Add(parsed);
            }
        }

        return results;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "A tolerant parser must skip any malformed individual event.")]
    private static ParsedIcsEvent? TryParseEvent(CalendarEvent calendarEvent)
    {
        try
        {
            if (calendarEvent.DtStart?.Value is not DateTime startValue)
            {
                return null;
            }

            var start = DateOnly.FromDateTime(startValue);

            DateOnly endInclusive;
            if (calendarEvent.DtEnd?.Value is DateTime endValue)
            {
                // All-day DTEND is exclusive; date-time events block through the end date.
                endInclusive = calendarEvent.IsAllDay
                    ? DateOnly.FromDateTime(endValue).AddDays(-1)
                    : DateOnly.FromDateTime(endValue);
            }
            else
            {
                endInclusive = start;
            }

            if (endInclusive < start)
            {
                endInclusive = start;
            }

            var uid = string.IsNullOrWhiteSpace(calendarEvent.Uid)
                ? $"{start:yyyyMMdd}-{endInclusive:yyyyMMdd}"
                : calendarEvent.Uid.Trim();

            return new ParsedIcsEvent(uid, start, endInclusive, calendarEvent.Summary);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
