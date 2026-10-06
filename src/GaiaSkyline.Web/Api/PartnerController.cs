using System.Security.Claims;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GaiaSkyline.Web.Api;

public sealed record PartnerTokenRequest(string Email, string Password);

public sealed record PartnerRefreshRequest(string RefreshToken);

/// <summary>
/// Partner JWT API: 15-minute access tokens and 7-day rotating refresh tokens with reuse detection.
/// Stateless bearer auth (no cookie, no antiforgery). The onboarding flow itself arrives in Stage 8.
/// </summary>
[ApiController]
[Route("api/partner")]
public sealed class PartnerController(
    UserManager<ApplicationUser> userManager,
    IPartnerRefreshTokenStore refreshTokens,
    JwtTokenFactory jwt,
    IAuditLog audit) : ControllerBase
{
    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Token([FromBody] PartnerTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null
            || !await userManager.IsInRoleAsync(user, UserRoles.Partner)
            || !await userManager.IsEmailConfirmedAsync(user)
            || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            await audit.WriteAsync("partner.token.denied", user?.Id, Ip, details: new { request.Email }, cancellationToken: cancellationToken);
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var refresh = await refreshTokens.IssueAsync(user.Id, cancellationToken);
        var (access, accessExpires) = jwt.CreateAccessToken(user.Id, user.Email!);
        await audit.WriteAsync("partner.token.issued", user.Id, Ip, cancellationToken: cancellationToken);

        return Ok(TokenResponse(access, accessExpires, refresh));
    }

    [HttpPost("token/refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh([FromBody] PartnerRefreshRequest request, CancellationToken cancellationToken)
    {
        var rotation = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotation is null)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        var user = await userManager.FindByIdAsync(rotation.UserId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        var (access, accessExpires) = jwt.CreateAccessToken(user.Id, user.Email!);
        return Ok(TokenResponse(access, accessExpires, rotation));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.PartnerJwt, Roles = UserRoles.Partner)]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
        });
    }

    /// <summary>Stage 8 Part A: clicks/bookings/value/commission for a period, default the current month.</summary>
    [HttpGet("stats")]
    [Authorize(AuthenticationSchemes = AuthSchemes.PartnerJwt, Roles = UserRoles.Partner)]
    public async Task<IActionResult> Stats(
        [FromQuery] string? period,
        [FromServices] GaiaSkyline.Application.Partners.IPartnerDashboardService dashboard,
        CancellationToken cancellationToken)
    {
        // period = yyyy-MM (e.g. 2026-10); anything else falls back to the current UTC month.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = new DateOnly(today.Year, today.Month, 1);
        if (period is not null
            && DateOnly.TryParseExact($"{period}-01", "yyyy-MM-dd", out var parsed))
        {
            from = parsed;
        }

        var stats = await dashboard.GetStatsAsync(RequireUserId(), from, from.AddMonths(1), cancellationToken);
        return stats is null ? NotFound() : Ok(new
        {
            period = $"{from:yyyy-MM}",
            clicks = stats.Clicks,
            bookings = stats.Bookings,
            bookingValueEur = stats.BookingValueEur,
            commissionEur = stats.CommissionEur,
        });
    }

    [HttpGet("bookings")]
    [Authorize(AuthenticationSchemes = AuthSchemes.PartnerJwt, Roles = UserRoles.Partner)]
    public async Task<IActionResult> Bookings(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromServices] GaiaSkyline.Application.Partners.IPartnerDashboardService dashboard,
        CancellationToken cancellationToken)
    {
        var rows = await dashboard.GetBookingsAsync(RequireUserId(), from, to, cancellationToken);
        return rows is null ? NotFound() : Ok(rows);
    }

    [HttpGet("payouts")]
    [Authorize(AuthenticationSchemes = AuthSchemes.PartnerJwt, Roles = UserRoles.Partner)]
    public async Task<IActionResult> Payouts(
        [FromServices] GaiaSkyline.Application.Partners.IPartnerDashboardService dashboard,
        CancellationToken cancellationToken)
    {
        var rows = await dashboard.GetPayoutsAsync(RequireUserId(), cancellationToken);
        return rows is null ? NotFound() : Ok(rows);
    }

    private Guid RequireUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static object TokenResponse(string access, DateTime accessExpires, RefreshRotation refresh) => new
    {
        accessToken = access,
        accessTokenExpiresAtUtc = accessExpires,
        refreshToken = refresh.RawToken,
        refreshTokenExpiresAtUtc = refresh.ExpiresAtUtc,
    };
}
