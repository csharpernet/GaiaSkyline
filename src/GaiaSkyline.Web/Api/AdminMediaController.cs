using System.Security.Claims;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Owner media writes: upload (responsive renditions) and collection item management. Cookie-auth +
/// SameSite=Lax (CSRF covered). Every write bumps the content revision and is audited.
/// </summary>
[ApiController]
[Route("api/admin/media")]
[Authorize(Policy = AuthorizationPolicies.Owner)]
public sealed class AdminMediaController(
    IAdminMediaService media,
    IHeroVideoService heroVideo,
    IWebHostEnvironment environment,
    IAuditLog audit) : ControllerBase
{
    // 500 MB — the hero-video source upload limit (ADR 0017 / Stage 7E-4).
    private const long HeroUploadByteLimit = 500L * 1024 * 1024;

    private string Actor => User.Identity?.Name ?? "owner";

    private Guid? ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? altText, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only image uploads are supported." });
        }

        var directory = Path.Combine(
            string.IsNullOrEmpty(environment.WebRootPath) ? "wwwroot" : environment.WebRootPath, "media");

        await using var stream = file.OpenReadStream();
        // The uploaded file name seeds the SEO filename (slugified + deduped); the owner can rename it later.
        var id = await media.UploadImageAsync(stream, directory, altText, file.FileName, Actor, cancellationToken);
        await audit.WriteAsync("media.upload", ActorId, Ip, "MediaAsset", id.ToString(), null, cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("hero")]
    [RequestSizeLimit(HeroUploadByteLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = HeroUploadByteLimit)]
    public async Task<IActionResult> UploadHero(
        IFormFile file,
        [FromForm] double? trimStart,
        [FromForm] double? trimEnd,
        [FromForm] double? crossfade,
        [FromForm] double? focalX,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var request = new HeroVideoUploadRequest(
            stream, file.FileName, file.Length, file.ContentType,
            trimStart, trimEnd, crossfade ?? 0, focalX ?? 0.5);
        var result = await heroVideo.StartUploadAsync(request, Actor, cancellationToken);
        if (!result.Accepted)
        {
            return BadRequest(new { error = result.Error });
        }

        await audit.WriteAsync("hero.upload", ActorId, Ip, "HeroVideo", result.HeroVideoId!.ToString(), null, cancellationToken);
        return Ok(new { id = result.HeroVideoId, status = "transcoding" });
    }

    [HttpPut("collections/{key}/items")]
    public async Task<IActionResult> SetItems(string key, [FromBody] IReadOnlyList<CollectionItemDto> items, CancellationToken cancellationToken)
    {
        if (!await media.SetCollectionItemsAsync(key, items ?? [], Actor, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("media.collection.set", ActorId, Ip, "MediaCollection", key, new { count = items?.Count ?? 0 }, cancellationToken);
        return NoContent();
    }
}
