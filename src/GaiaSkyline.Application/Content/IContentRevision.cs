namespace GaiaSkyline.Application.Content;

/// <summary>
/// A monotonic counter bumped whenever any content block or translation is written. It forms part
/// of the content cache key, so a bump transparently invalidates every cached section/language.
/// </summary>
public interface IContentRevision
{
    long Current { get; }

    void Bump();
}
