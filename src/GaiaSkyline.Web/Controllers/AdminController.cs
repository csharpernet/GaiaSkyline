using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The Owner admin surface (English, not localized, noindex). Password sign-in is always followed by
/// TOTP: enrolment is forced on first login, and the challenge is required thereafter. "Remember this
/// browser" is never offered to the Owner.
/// </summary>
[Route("admin")]
[OutputCache(NoStore = true)]
public sealed class AdminController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditLog audit) : Controller
{
    private const string Issuer = "Gaia Skyline";

    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        NoIndex();
        return View(new AdminLoginViewModel());
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        NoIndex();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null || !await userManager.IsInRoleAsync(user, UserRoles.Owner))
        {
            await audit.WriteAsync("admin.login.denied", user?.Id, Ip, details: new { model.Email });
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(TwoFactor));
        }

        if (result.Succeeded)
        {
            // Owner has no authenticator yet — force enrolment before granting access.
            await audit.WriteAsync("admin.login.password_ok", user.Id, Ip);
            return RedirectToAction(nameof(EnrollTwoFactor));
        }

        if (result.IsLockedOut)
        {
            await audit.WriteAsync("admin.login.lockout", user.Id, Ip);
            ModelState.AddModelError(string.Empty, "This account is locked for 15 minutes after too many attempts.");
            return View(model);
        }

        await audit.WriteAsync("admin.login.failure", user.Id, Ip);
        ModelState.AddModelError(string.Empty, "Invalid credentials.");
        return View(model);
    }

    [HttpGet("two-factor")]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFactor()
    {
        NoIndex();
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new TwoFactorViewModel());
    }

    [HttpPost("two-factor")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> TwoFactor(TwoFactorViewModel model)
    {
        NoIndex();
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var code = model.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
                             .Replace("-", string.Empty, StringComparison.Ordinal);

        // rememberClient is always false — the Owner's browser is never remembered.
        var result = model.IsRecoveryCode
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);

        if (result.Succeeded)
        {
            await audit.WriteAsync(model.IsRecoveryCode ? "2fa.recovery_used" : "2fa.success", user.Id, Ip);
            return LocalRedirect("/admin");
        }

        if (result.IsLockedOut)
        {
            await audit.WriteAsync("2fa.lockout", user.Id, Ip);
            ModelState.AddModelError(string.Empty, "This account is temporarily locked.");
            return View(model);
        }

        await audit.WriteAsync("2fa.failure", user.Id, Ip);
        ModelState.AddModelError(string.Empty, "Invalid code.");
        return View(model);
    }

    [HttpGet("enroll-2fa")]
    public async Task<IActionResult> EnrollTwoFactor()
    {
        NoIndex();
        var user = await userManager.GetUserAsync(User) ?? await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(await BuildEnrollModelAsync(user));
    }

    [HttpPost("enroll-2fa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollTwoFactor(EnrollTwoFactorViewModel model)
    {
        NoIndex();
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var code = model.Code.Replace(" ", string.Empty, StringComparison.Ordinal)
                             .Replace("-", string.Empty, StringComparison.Ordinal);
        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), "That code didn't match. Try again.");
            return View(await BuildEnrollModelAsync(user));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await audit.WriteAsync("2fa.enabled", user.Id, Ip);

        return View("RecoveryCodes", recoveryCodes?.ToArray() ?? []);
    }

    private async Task<EnrollTwoFactorViewModel> BuildEnrollModelAsync(ApplicationUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = user.Email ?? user.UserName ?? "owner";
        var otpauth = string.Format(
            CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            UrlEncoder.Default.Encode(Issuer),
            UrlEncoder.Default.Encode(email),
            key);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauth, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data).GetGraphic(4);

        return new EnrollTwoFactorViewModel { SharedKey = FormatKey(key!), QrSvg = svg };
    }

    private static string FormatKey(string key)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            builder.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }

        return builder.ToString().Trim().ToLowerInvariant();
    }

    private void NoIndex() => ViewData["NoIndex"] = true;
}
