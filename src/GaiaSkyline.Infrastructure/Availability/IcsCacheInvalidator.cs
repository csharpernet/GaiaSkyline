using System.Threading;
using GaiaSkyline.Application.Availability;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>
/// Real ICS export cache seam (replaces the Increment-C no-op). Holds a process-wide version that the
/// export service uses as its cache key and ETag; <see cref="Invalidate"/> bumps it so the next export
/// request regenerates. Bumped on every booking and owner-block change.
/// </summary>
public sealed class IcsCacheInvalidator : IIcsCacheInvalidator
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void Invalidate() => Interlocked.Increment(ref _version);
}
