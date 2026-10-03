using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Availability;

/// <summary>
/// An imported external-calendar block that overlaps an active direct booking. Recorded once per
/// (booking, source, range) so the owner is alerted a single time; never auto-cancels anything.
/// </summary>
public sealed class BookingConflict
{
    // Required by EF Core's materialization.
    private BookingConflict()
    {
    }

    public BookingConflict(
        BookingConflictId id, string bookingReference, string sourceName,
        DateOnly startDate, DateOnly endDate, DateTime detectedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        Id = id;
        BookingReference = bookingReference.Trim().ToUpperInvariant();
        SourceName = sourceName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        DetectedAtUtc = detectedAtUtc;
    }

    public BookingConflictId Id { get; private set; }

    public string BookingReference { get; private set; } = null!;

    public string SourceName { get; private set; } = null!;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public DateTime DetectedAtUtc { get; private set; }
}
