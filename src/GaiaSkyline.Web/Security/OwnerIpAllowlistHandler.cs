using System.Net;
using Microsoft.AspNetCore.Authorization;

namespace GaiaSkyline.Web.Security;

/// <summary>Requires the request to originate from an allowlisted IP (added to the Owner policy).</summary>
public sealed class OwnerIpAllowlistRequirement : IAuthorizationRequirement;

/// <summary>
/// Enforces <c>Owner:AllowedIps</c>. An empty list means no restriction (the Development default); any
/// non-empty list restricts the Owner surface to those exact addresses.
/// </summary>
public sealed class OwnerIpAllowlistHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<OwnerIpAllowlistHandler> logger) : AuthorizationHandler<OwnerIpAllowlistRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerIpAllowlistRequirement requirement)
    {
        var allowed = configuration.GetSection("Owner:AllowedIps").Get<string[]>() ?? [];
        if (allowed.Length == 0)
        {
            // No allowlist configured → no IP restriction (Development).
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var remote = Normalize(httpContextAccessor.HttpContext?.Connection.RemoteIpAddress);
        if (remote is not null)
        {
            foreach (var entry in allowed)
            {
                if (IPAddress.TryParse(entry, out var parsed) && Normalize(parsed)!.Equals(remote))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        logger.LogWarning("Owner access denied for IP {RemoteIp}: not in the allowlist.", remote);
        return Task.CompletedTask;
    }

    private static IPAddress? Normalize(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;
}
