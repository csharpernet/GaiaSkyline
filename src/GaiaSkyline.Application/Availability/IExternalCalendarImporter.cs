namespace GaiaSkyline.Application.Availability;

/// <summary>
/// Imports all enabled external calendars over iCal. With no enabled sources it logs once and returns
/// (manual mode — not a failure). Invoked by the Hangfire sync job; dormant until a source is configured.
/// </summary>
public interface IExternalCalendarImporter
{
    Task ImportAllAsync(CancellationToken cancellationToken);
}
