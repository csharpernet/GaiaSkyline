namespace GaiaSkyline.Application.Content;

/// <summary>In-memory singleton implementation of <see cref="IContentRevision"/>.</summary>
public sealed class ContentRevision : IContentRevision
{
    private long _value;

    public long Current => Interlocked.Read(ref _value);

    public void Bump() => Interlocked.Increment(ref _value);
}
