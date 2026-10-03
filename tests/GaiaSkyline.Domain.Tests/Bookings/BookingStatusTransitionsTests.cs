using FluentAssertions;
using GaiaSkyline.Domain.Bookings;
using static GaiaSkyline.Domain.Bookings.BookingStatus;

namespace GaiaSkyline.Domain.Tests.Bookings;

public class BookingStatusTransitionsTests
{
    // The single source of truth the production table is checked against. Every (from,to) pair not
    // listed here — including every self-edge — must be rejected.
    private static readonly Dictionary<BookingStatus, BookingStatus[]> Legal = new()
    {
        [AwaitingPayment] = [Confirmed, Cancelled],
        [Confirmed] = [CheckedIn, Cancelled, Refunded, PartiallyRefunded],
        [CheckedIn] = [Completed, Refunded, PartiallyRefunded],
        [Completed] = [Refunded, PartiallyRefunded],
        [Cancelled] = [Refunded, PartiallyRefunded],
        [PartiallyRefunded] = [Refunded],
        [Refunded] = [],
    };

    public static IEnumerable<object[]> AllPairs() =>
        from source in Enum.GetValues<BookingStatus>()
        from target in Enum.GetValues<BookingStatus>()
        select new object[] { source, target };

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void CanTransition_matches_the_table(BookingStatus from, BookingStatus to)
    {
        var expected = Legal[from].Contains(to);

        BookingStatusTransitions.CanTransition(from, to).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void EnsureCanTransition_throws_for_every_illegal_edge(BookingStatus from, BookingStatus to)
    {
        var act = () => BookingStatusTransitions.EnsureCanTransition(from, to);

        if (Legal[from].Contains(to))
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidBookingStatusTransitionException>();
        }
    }

    [Fact]
    public void No_status_may_transition_to_itself()
    {
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            BookingStatusTransitions.CanTransition(status, status).Should().BeFalse($"{status} → {status} is a no-op");
        }
    }

    [Fact]
    public void Refunded_is_terminal()
    {
        foreach (var to in Enum.GetValues<BookingStatus>())
        {
            BookingStatusTransitions.CanTransition(Refunded, to).Should().BeFalse();
        }
    }
}
