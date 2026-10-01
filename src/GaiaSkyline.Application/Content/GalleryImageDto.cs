namespace GaiaSkyline.Application.Content;

/// <summary>A media asset within a collection (e.g. the home gallery), with its order and hero flag.</summary>
public sealed record GalleryImageDto(MediaAssetDto Asset, int DisplayOrder, bool IsHero);
