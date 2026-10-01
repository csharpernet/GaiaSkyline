namespace GaiaSkyline.Application.Content;

/// <summary>
/// The resolved content for one section in one language — a map of block key to resolved value.
/// This is what the whole public site reads.
/// </summary>
public sealed record ContentPayload
{
    public required string Section { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyDictionary<string, ContentValue> Items { get; init; }
}
