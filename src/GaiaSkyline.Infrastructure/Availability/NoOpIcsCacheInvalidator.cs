using GaiaSkyline.Application.Availability;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>No-op ICS cache seam for Increment C; the real exporter/cache ships with Stage 5.</summary>
internal sealed class NoOpIcsCacheInvalidator : IIcsCacheInvalidator
{
    public void Invalidate()
    {
        // Intentionally empty until the ICS export cache exists (Stage 5).
    }
}
