using FluentAssertions;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Tests.ValueObjects;

public class DateRangeTests
{
    private static DateOnly D(int day) => new(2026, 1, day);

    [Fact]
    public void Valid_range_exposes_start_end_and_nights()
    {
        var range = new DateRange(D(1), D(4));

        range.Start.Should().Be(D(1));
        range.End.Should().Be(D(4));
        range.Nights.Should().Be(3);
    }

    [Fact]
    public void Rejects_zero_night_range()
    {
        var act = () => new DateRange(D(1), D(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_negative_range()
    {
        var act = () => new DateRange(D(5), D(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Overlapping_ranges_are_detected()
    {
        var a = new DateRange(D(1), D(5));
        var b = new DateRange(D(4), D(8));

        a.Overlaps(b).Should().BeTrue();
        b.Overlaps(a).Should().BeTrue();
    }

    [Fact]
    public void Adjacent_ranges_do_not_overlap_because_checkout_is_exclusive()
    {
        // Guest A: nights 1-4 (checkout on the 5th). Guest B checks in on the 5th.
        var a = new DateRange(D(1), D(5));
        var b = new DateRange(D(5), D(8));

        a.Overlaps(b).Should().BeFalse();
        b.Overlaps(a).Should().BeFalse();
    }

    [Fact]
    public void Disjoint_ranges_do_not_overlap()
    {
        var a = new DateRange(D(1), D(5));
        var b = new DateRange(D(6), D(9));

        a.Overlaps(b).Should().BeFalse();
    }

    [Fact]
    public void Identical_ranges_overlap()
    {
        var a = new DateRange(D(1), D(5));

        a.Overlaps(a).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]   // start is inclusive
    [InlineData(4, true)]   // within
    [InlineData(5, false)]  // end is exclusive
    [InlineData(6, false)]  // outside
    public void Contains_treats_end_as_exclusive(int day, bool expected)
    {
        var range = new DateRange(D(1), D(5));

        range.Contains(D(day)).Should().Be(expected);
    }

    [Fact]
    public void Equality_considers_both_endpoints()
    {
        new DateRange(D(1), D(5)).Should().Be(new DateRange(D(1), D(5)));
        new DateRange(D(1), D(5)).Should().NotBe(new DateRange(D(1), D(6)));
    }
}
