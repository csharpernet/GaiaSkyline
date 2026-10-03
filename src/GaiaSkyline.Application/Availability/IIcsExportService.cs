namespace GaiaSkyline.Application.Availability;

/// <summary>The exported calendar bytes plus a content ETag for conditional requests.</summary>
public sealed record IcsExportResult(byte[] Content, string ETag);

/// <summary>
/// Builds the public .ics feed external platforms subscribe to: one all-day event per active direct
/// booking plus owner "unavailable" holds. External-booking owner blocks are excluded (they already
/// live in the management system and would echo back). No guest data. Cached until invalidated.
/// </summary>
public interface IIcsExportService
{
    Task<IcsExportResult> GetAsync(CancellationToken cancellationToken);
}
