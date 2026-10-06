using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;

namespace GaiaSkyline.Web.Models;

/// <summary>One language's fields posted from the story editor.</summary>
public sealed class StoryTranslationForm
{
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>This language's slug (non-default languages; blank regenerates from the language's title).</summary>
    public string? Slug { get; set; }

    public string? Title { get; set; }

    public string? Excerpt { get; set; }

    public string? Body { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }
}

/// <summary>The whole story editor form (create or update).</summary>
public sealed class StoryForm
{
    public string? Slug { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public Guid CoverMediaAssetId { get; set; }

    public DateTime PublishedDate { get; set; }

    public bool IsPublished { get; set; }

    public List<StoryTranslationForm> Translations { get; set; } = [];
}

/// <summary>View model for the story editor (new when <see cref="Id"/> is null), with the cover-picker images.</summary>
public sealed record StoryEditViewModel(
    Guid? Id,
    string? Slug,
    string AuthorName,
    Guid CoverMediaAssetId,
    DateTime PublishedDate,
    bool IsPublished,
    IReadOnlyList<StoryTranslationEditDto> Translations,
    IReadOnlyList<string> PreviousSlugs,
    IReadOnlyList<MediaLibraryItemDto> Images);
