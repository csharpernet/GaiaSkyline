using GaiaSkyline.Application.Content;

namespace GaiaSkyline.Web;

/// <summary>Convenience accessors for reading resolved values out of a <see cref="ContentPayload"/> in views/controllers.</summary>
public static class ContentPayloadExtensions
{
    public static ContentValue? Value(this ContentPayload payload, string key) =>
        payload.Items.TryGetValue(key, out var value) ? value : null;

    public static string? Text(this ContentPayload payload, string key) => payload.Value(key)?.Text;

    public static decimal? Number(this ContentPayload payload, string key) => payload.Value(key)?.Number;

    public static bool? Boolean(this ContentPayload payload, string key) => payload.Value(key)?.Boolean;

    public static MediaAssetDto? Media(this ContentPayload payload, string key) => payload.Value(key)?.Media;

    public static string TextOr(this ContentPayload payload, string key, string fallback)
    {
        var text = payload.Text(key);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
