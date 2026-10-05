using FluentAssertions;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Tests.Availability;

public sealed class BookingConflictTests
{
    private static BookingConflict Conflict() => new(
        BookingConflictId.New(), "gs-c1", "Hostify",
        new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 4),
        new DateTime(2027, 4, 1, 8, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Starts_open_and_resolve_records_when_and_how()
    {
        var conflict = Conflict();
        conflict.IsResolved.Should().BeFalse();

        var at = new DateTime(2027, 4, 2, 9, 0, 0, DateTimeKind.Utc);
        conflict.Resolve(at, "  guest moved to other dates  ");

        conflict.IsResolved.Should().BeTrue();
        conflict.ResolvedAtUtc.Should().Be(at);
        conflict.ResolvedNote.Should().Be("guest moved to other dates");
    }

    [Fact]
    public void Resolve_treats_a_blank_note_as_none_and_can_update_an_earlier_resolution()
    {
        var conflict = Conflict();
        conflict.Resolve(new DateTime(2027, 4, 2, 9, 0, 0, DateTimeKind.Utc), "   ");
        conflict.ResolvedNote.Should().BeNull();

        var later = new DateTime(2027, 4, 3, 9, 0, 0, DateTimeKind.Utc);
        conflict.Resolve(later, "Hostify block removed");
        conflict.ResolvedAtUtc.Should().Be(later);
        conflict.ResolvedNote.Should().Be("Hostify block removed");
    }

    [Fact]
    public void Reference_is_normalized_uppercase()
    {
        Conflict().BookingReference.Should().Be("GS-C1");
    }
}
