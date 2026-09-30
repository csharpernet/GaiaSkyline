namespace GaiaSkyline.Domain.ValueObjects;

/// <summary>
/// A half-open range of calendar dates <c>[Start, End)</c> following the iCal convention:
/// the checkout date (<see cref="End"/>) is exclusive. A range must span at least one night.
/// </summary>
public readonly record struct DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException(
                "The end date cannot be before the start date.", nameof(end));
        }

        if (end == start)
        {
            throw new ArgumentException(
                "A date range must span at least one night (checkout is exclusive).", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>The first night (inclusive).</summary>
    public DateOnly Start { get; }

    /// <summary>The checkout date (exclusive).</summary>
    public DateOnly End { get; }

    /// <summary>Number of nights in the range. Always &gt;= 1.</summary>
    public int Nights => End.DayNumber - Start.DayNumber;

    /// <summary>
    /// Whether this range overlaps <paramref name="other"/>. Because checkout is exclusive,
    /// a range ending on the same day another begins does NOT overlap
    /// (guest A checks out, guest B checks in).
    /// </summary>
    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;

    /// <summary>Whether <paramref name="date"/> falls within <c>[Start, End)</c>.</summary>
    public bool Contains(DateOnly date) => date >= Start && date < End;

    public override string ToString() => $"[{Start:yyyy-MM-dd}, {End:yyyy-MM-dd})";
}
