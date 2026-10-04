namespace GaiaSkyline.Application.Content;

/// <summary>
/// Whether the current request is an authenticated owner preview (a valid, signed preview cookie). In
/// preview the content read returns draft values and unpublished blocks, and the output cache is bypassed.
/// Implemented in the Web layer; defaults to "not preview" everywhere else.
/// </summary>
public interface IContentPreviewState
{
    bool IsPreview { get; }
}

/// <summary>Default: never in preview (used outside the web request pipeline, e.g. background jobs, tests).</summary>
public sealed class NoPreviewState : IContentPreviewState
{
    public bool IsPreview => false;
}
