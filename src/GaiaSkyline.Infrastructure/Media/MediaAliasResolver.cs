using GaiaSkyline.Application.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Maps a previous media filename stem to the owning asset's current stem. Only hit on a <c>/media/</c> miss
/// (the static-file handler already served every live file), so a per-request lookup is fine. Stage 7E-2d.
/// </summary>
internal sealed class MediaAliasResolver(AppDbContext dbContext) : IMediaAliasResolver
{
    public async Task<string?> ResolveCurrentStemAsync(string oldStem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oldStem))
        {
            return null;
        }

        var stem = oldStem.Trim();
        var alias = await dbContext.MediaAssetAliases
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.OldSlug == stem, cancellationToken);
        if (alias is null)
        {
            return null;
        }

        // Compare the whole strongly-typed id (the converter handles equality); never project .Value into SQL.
        var id = alias.MediaAssetId;
        var blobUri = await dbContext.MediaAssets
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.BlobUri)
            .FirstOrDefaultAsync(cancellationToken);

        return MediaStem.Of(blobUri);
    }
}
