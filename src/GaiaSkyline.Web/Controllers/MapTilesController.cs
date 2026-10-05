using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Same-origin proxy for the location map's OpenStreetMap raster tiles (Stage 7 §5 / ADR 0018). The browser only
/// ever talks to our origin, so the Content-Security-Policy needs no external tile host. Tiles are fetched
/// server-side with a descriptive User-Agent (the OSM tile usage policy requires one) and cached hard; OSM
/// attribution is still shown on the map. Coordinates are range-checked so the proxy can only request a real
/// tile (no SSRF surface).
/// </summary>
public sealed class MapTilesController(IHttpClientFactory httpClientFactory, ILogger<MapTilesController> logger) : ControllerBase
{
    /// <summary>Named <see cref="System.Net.Http.HttpClient"/> (configured in Program.cs) that targets the OSM tile host.</summary>
    public const string HttpClientName = "osm-tiles";

    private const int MaxZoom = 19;
    private const int CacheSeconds = 604800; // 7 days — our fixed location's tiles are effectively immutable.

    [HttpGet("/map/tiles/{z:int}/{x:int}/{y:int}.png")]
    [OutputCache(Duration = CacheSeconds)]
    public async Task<IActionResult> Tile(int z, int x, int y, CancellationToken cancellationToken)
    {
        if (z < 0 || z > MaxZoom)
        {
            return NotFound();
        }

        var tilesPerAxis = 1 << z;
        if (x < 0 || x >= tilesPerAxis || y < 0 || y >= tilesPerAxis)
        {
            return NotFound();
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var upstream = await client.GetAsync($"{z}/{x}/{y}.png", cancellationToken);
            if (!upstream.IsSuccessStatusCode)
            {
                logger.LogWarning("OSM tile {Z}/{X}/{Y} responded {Status}.", z, x, y, (int)upstream.StatusCode);
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            var bytes = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);
            Response.Headers.CacheControl = $"public, max-age={CacheSeconds}, immutable";
            return File(bytes, "image/png");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not fetch OSM tile {Z}/{X}/{Y}.", z, x, y);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
