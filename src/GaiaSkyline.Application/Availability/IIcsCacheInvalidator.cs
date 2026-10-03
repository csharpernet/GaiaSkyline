namespace GaiaSkyline.Application.Availability;

/// <summary>
/// Invalidates the exported-calendar (.ics) cache after a booking change so external platforms see
/// the updated availability. The real implementation ships with ICS export in Stage 5; Increment C
/// calls this seam with a no-op stub.
/// </summary>
public interface IIcsCacheInvalidator
{
    void Invalidate();
}
