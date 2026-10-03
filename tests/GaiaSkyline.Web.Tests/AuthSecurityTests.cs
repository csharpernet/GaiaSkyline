using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class AuthSecurityTests(PublicSiteFactory factory)
{
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string NewPassword() => $"Zx9-{Guid.NewGuid():N}";

    // ---- Owner IP allowlist ----

    [Fact]
    public async Task Owner_ip_allowlist_allows_when_empty_and_blocks_non_listed_ips()
    {
        // Empty list → no restriction (Development default).
        (await EvaluateAllowlistAsync([], "203.0.113.9")).Should().BeTrue();

        // Listed IP → allowed; other IP → blocked.
        (await EvaluateAllowlistAsync(["203.0.113.5"], "203.0.113.5")).Should().BeTrue();
        (await EvaluateAllowlistAsync(["203.0.113.5"], "203.0.113.9")).Should().BeFalse();
    }

    private static async Task<bool> EvaluateAllowlistAsync(string[] allowed, string remoteIp)
    {
        var config = new Dictionary<string, string?>();
        for (var i = 0; i < allowed.Length; i++)
        {
            config[$"Owner:AllowedIps:{i}"] = allowed[i];
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var handler = new OwnerIpAllowlistHandler(accessor, configuration, NullLogger<OwnerIpAllowlistHandler>.Instance);
        var requirement = new OwnerIpAllowlistRequirement();
        var context = new AuthorizationHandlerContext([requirement], httpContext.User, null);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    // ---- Owner 2FA (TOTP + recovery codes) ----

    [Fact]
    public async Task Totp_verifies_valid_codes_rejects_invalid_and_recovery_codes_are_single_use()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = NewEmail(), Email = NewEmail(), EmailConfirmed = true };
        user.Email = user.UserName;
        (await userManager.CreateAsync(user, NewPassword())).Succeeded.Should().BeTrue();

        await userManager.ResetAuthenticatorKeyAsync(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        // The authenticator provider only validates; a real code is computed from the key as an app would.
        var validCode = ComputeTotp(key!);

        (await userManager.VerifyTwoFactorTokenAsync(user, "Authenticator", validCode)).Should().BeTrue();
        (await userManager.VerifyTwoFactorTokenAsync(user, "Authenticator", "000000")).Should().BeFalse();

        var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.ToArray();
        codes.Should().HaveCount(10);

        (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, codes[0])).Succeeded.Should().BeTrue();
        // Single use: redeeming the same code again fails.
        (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, codes[0])).Succeeded.Should().BeFalse();
    }

    // ---- Lockout ----

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = NewEmail(), EmailConfirmed = true };
        user.Email = user.UserName;
        (await userManager.CreateAsync(user, NewPassword())).Succeeded.Should().BeTrue();

        for (var i = 0; i < 5; i++)
        {
            await userManager.AccessFailedAsync(user);
        }

        (await userManager.IsLockedOutAsync(user)).Should().BeTrue();
    }

    // ---- Partner JWT end to end ----

    [Fact]
    public async Task Partner_can_obtain_a_token_and_call_me()
    {
        var email = NewEmail();
        var password = NewPassword();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(user, UserRoles.Partner)).Succeeded.Should().BeTrue();
        }

        using var client = factory.CreateClient();

        using var tokenResponse = await client.PostAsJsonAsync(
            new Uri("/api/partner/token", UriKind.Relative), new { email, password });
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken.Should().NotBeNullOrEmpty();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/partner/me", UriKind.Relative));
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var meResponse = await client.SendAsync(meRequest);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        me!.Email.Should().Be(email);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken);

    private sealed record MeResponse(string Id, string Email);

    // RFC 6238 TOTP over the authenticator key (HMACSHA1, 30s step, 6 digits) — matches what an
    // authenticator app produces and what ASP.NET Identity's authenticator provider validates.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350",
        Justification = "HMAC-SHA1 is mandated by the TOTP (RFC 6238) / authenticator-app standard.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351",
        Justification = "HMAC-SHA1 is mandated by the TOTP (RFC 6238) / authenticator-app standard.")]
    private static string ComputeTotp(string base32Key)
    {
        var keyBytes = Base32Decode(base32Key);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(message);
        }

        using var hmac = new System.Security.Cryptography.HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(message);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int bits = 0, value = 0;
        foreach (var c in cleaned)
        {
            value = (value << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
