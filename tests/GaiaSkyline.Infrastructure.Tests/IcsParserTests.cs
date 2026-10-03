using FluentAssertions;
using GaiaSkyline.Infrastructure.Availability;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class IcsParserTests
{
    // Hostify-style feed: all-day reservations, one with a full UID + SUMMARY, one missing both.
    // NOTE: replace with a real Hostify export when one is available (see docs/runbook.md).
    private const string HostifyFeed =
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Hostify//EN\r\n" +
        "BEGIN:VEVENT\r\nUID:resv-12345@hostify.com\r\nDTSTART;VALUE=DATE:20280701\r\nDTEND;VALUE=DATE:20280705\r\n" +
        "SUMMARY:Reserved\r\nEND:VEVENT\r\n" +
        "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20280710\r\nDTEND;VALUE=DATE:20280712\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    // Airbnb-style feed: all-day, opaque UID, "Not available" summary.
    private const string AirbnbFeed =
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Airbnb Inc//Hosting Calendar//EN\r\nCALSCALE:GREGORIAN\r\n" +
        "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20280801\r\nDTEND;VALUE=DATE:20280803\r\n" +
        "UID:1234567890abcdef@airbnb.com\r\nSUMMARY:Airbnb (Not available)\r\nEND:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    [Fact]
    public void Parses_hostify_feed_with_inclusive_ends_and_synthesizes_missing_uids()
    {
        var events = IcsParser.Parse(HostifyFeed);

        events.Should().HaveCount(2);

        var reserved = events.Single(e => e.Uid == "resv-12345@hostify.com");
        reserved.Start.Should().Be(new DateOnly(2028, 7, 1));
        reserved.EndInclusive.Should().Be(new DateOnly(2028, 7, 4)); // DTEND 5th is exclusive
        reserved.Summary.Should().Be("Reserved");

        // The event with no SUMMARY still parses (null summary); it always gets a non-empty UID
        // (Ical.Net assigns one when the feed omits it), so upsert has a key to work with.
        var other = events.Single(e => e.Uid != "resv-12345@hostify.com");
        other.Start.Should().Be(new DateOnly(2028, 7, 10));
        other.EndInclusive.Should().Be(new DateOnly(2028, 7, 11));
        other.Summary.Should().BeNull();
        other.Uid.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parses_airbnb_feed()
    {
        var events = IcsParser.Parse(AirbnbFeed);

        events.Should().ContainSingle();
        var e = events[0];
        e.Uid.Should().Be("1234567890abcdef@airbnb.com");
        e.Start.Should().Be(new DateOnly(2028, 8, 1));
        e.EndInclusive.Should().Be(new DateOnly(2028, 8, 2));
        e.Summary.Should().Be("Airbnb (Not available)");
    }

    [Fact]
    public void Malformed_feed_yields_no_events_instead_of_throwing()
    {
        IcsParser.Parse("this is not a calendar").Should().BeEmpty();
        IcsParser.Parse(string.Empty).Should().BeEmpty();
    }
}
