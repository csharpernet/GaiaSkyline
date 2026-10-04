namespace GaiaSkyline.Application.Media;

/// <summary>
/// Resolves a previous media filename stem (kept after a rename) to the asset's current stem, so the media
/// URL middleware can 301 an old, possibly-indexed <c>/media/{oldStem}-{w}.{ext}</c> URL to the live file.
/// Returns null when the stem is not a known alias (a genuine 404). Stage 7E-2d.
/// </summary>
public interface IMediaAliasResolver
{
    Task<string?> ResolveCurrentStemAsync(string oldStem, CancellationToken cancellationToken);
}
