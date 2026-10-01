using GaiaSkyline.Domain.Content;

namespace GaiaSkyline.Application.Content;

/// <summary>
/// A single resolved content value for one language. Only the field(s) relevant to
/// <see cref="Kind"/> are populated. <see cref="ResolvedLanguage"/> records which language the
/// value actually came from ("en" when fallen back, "none" when the value is a ‹key› gap marker).
/// </summary>
public sealed record ContentValue
{
    public required ContentKind Kind { get; init; }

    public string? Text { get; init; }

    public decimal? Number { get; init; }

    public bool? Boolean { get; init; }

    public MediaAssetDto? Media { get; init; }

    public required string ResolvedLanguage { get; init; }
}
