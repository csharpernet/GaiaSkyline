namespace GaiaSkyline.Application.Content;

/// <summary>One language's content when creating/updating a story (body is sanitised server-side on save).</summary>
public sealed record StoryTranslationInput(
    string LanguageCode,
    string? Title,
    string? Excerpt,
    string? BodyRichText,
    string? MetaTitle,
    string? MetaDescription);

/// <summary>Everything the owner submits when creating or updating a story.</summary>
public sealed record StoryWriteModel(
    string? Slug,
    string AuthorName,
    Guid CoverMediaAssetId,
    DateTime PublishedAtUtc,
    bool IsPublished,
    IReadOnlyList<StoryTranslationInput> Translations);

/// <summary>Outcome of a create/update.</summary>
public sealed record StorySaveResult(bool Ok, Guid? StoryId, string? Error)
{
    public static StorySaveResult Success(Guid id) => new(true, id, null);

    public static StorySaveResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Owner-only writes for stories: create/update (per-language, slug auto-suggested + kept unique, reading time
/// computed and the body sanitised on save), publish/unpublish and delete. Renaming a published story's slug
/// records a redirect so the old URL 301s to the new one. Each write bumps the content revision.
/// </summary>
public interface IAdminStoryService
{
    Task<StorySaveResult> CreateAsync(StoryWriteModel model, string actor, CancellationToken cancellationToken);

    Task<StorySaveResult> UpdateAsync(Guid id, StoryWriteModel model, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, string actor, CancellationToken cancellationToken);
}
