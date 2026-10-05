using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Seo;

/// <summary>
/// Composes the admin media and story reads into a single SEO warnings list. Images need alt text in every
/// content language before they are publishable; a published story should be fully translated and carry a meta
/// description per language. Stage 7 §5.
/// </summary>
public sealed class SeoWarningsService(IAdminMediaReadService media, IAdminStoryReadService stories) : ISeoWarningsService
{
    public async Task<IReadOnlyList<SeoWarning>> GetWarningsAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<SeoWarning>();

        // Images missing alt text in one or more content languages (videos are decorative → skipped).
        var images = await media.GetLibraryAsync(MediaKind.Image, includeDeleted: false, cancellationToken);
        foreach (var image in images)
        {
            var missing = ContentLanguages.All
                .Where(l => !image.LanguagesWithAlt.Contains(l, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (missing.Count > 0)
            {
                warnings.Add(new SeoWarning(
                    "Alt text",
                    $"Image {Label(image.BlobUri)} has no alt text for {Join(missing)}.",
                    $"/admin/media/{image.Id}"));
            }
        }

        // Published stories: a missing translation or a missing meta description hurts SEO. GetForEditAsync fills
        // every language with empty strings where a translation is absent, so "missing" = a blank title.
        var list = await stories.GetAllAsync(cancellationToken);
        foreach (var story in list.Where(s => s.IsPublished))
        {
            var edit = await stories.GetForEditAsync(story.Id, cancellationToken);
            if (edit is null)
            {
                continue;
            }

            var missingTranslations = edit.Translations
                .Where(t => string.IsNullOrWhiteSpace(t.Title))
                .Select(t => t.LanguageCode)
                .ToList();
            if (missingTranslations.Count > 0)
            {
                warnings.Add(new SeoWarning(
                    "Story translation",
                    $"Story “{story.Slug}” has no translation for {Join(missingTranslations)}.",
                    $"/admin/stories/{story.Id}"));
            }

            // Only flag a missing meta description where the translation actually exists.
            foreach (var translation in edit.Translations
                .Where(t => !string.IsNullOrWhiteSpace(t.Title) && string.IsNullOrWhiteSpace(t.MetaDescription)))
            {
                warnings.Add(new SeoWarning(
                    "Story meta",
                    $"Story “{story.Slug}” ({ContentLanguages.ShortLabel(translation.LanguageCode)}) has no meta description.",
                    $"/admin/stories/{story.Id}"));
            }
        }

        return warnings;
    }

    private static string Join(IEnumerable<string> languages) =>
        string.Join(", ", languages.Select(ContentLanguages.ShortLabel));

    // A friendly label from a responsive blob URI, e.g. "/media/douro-sunset-1600.jpg" → "douro-sunset.jpg".
    private static string Label(string blobUri)
    {
        var name = Path.GetFileName(blobUri);
        var dot = name.LastIndexOf('.');
        if (dot <= 0)
        {
            return name;
        }

        var stem = name[..dot];
        var ext = name[dot..];
        var dash = stem.LastIndexOf('-');
        if (dash > 0 && stem[(dash + 1)..].All(char.IsDigit))
        {
            stem = stem[..dash];
        }

        return stem + ext;
    }
}
