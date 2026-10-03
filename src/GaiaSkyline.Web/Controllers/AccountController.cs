using System.Text;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Public, localized account pages for Guest and Partner accounts: login, register, password reset and
/// email confirmation. All pages are noindex and never cached. The Owner authenticates at /admin.
/// </summary>
[Route("{lang:culture}/account")]
[AllowAnonymous]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuthEmailService authEmail,
    IGuestMagicLinkService magicLinks,
    BookingAccessCookie bookingAccess,
    IAuditLog audit) : PublicController
{
    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpGet("login")]
    [OutputCache(NoStore = true)]
    public IActionResult Login(string? returnUrl = null)
    {
        SetAuthMeta("login", "Sign in");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        SetAuthMeta("login", "Sign in");
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null
            && await userManager.IsInRoleAsync(user, UserRoles.Partner)
            && !await userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Please confirm your email address before signing in.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await audit.WriteAsync("login.success", user?.Id, Ip, details: new { model.Email });
            return RedirectToLocalOrDefault(model.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            // Owner accounts complete 2FA on the admin surface.
            return LocalRedirect($"/admin/two-factor?returnUrl={Uri.EscapeDataString(model.ReturnUrl ?? string.Empty)}");
        }

        if (result.IsLockedOut)
        {
            await audit.WriteAsync("login.lockout", user?.Id, Ip, details: new { model.Email });
            ModelState.AddModelError(string.Empty, "This account is locked for 15 minutes after too many attempts.");
            return View(model);
        }

        await audit.WriteAsync("login.failure", user?.Id, Ip, details: new { model.Email });
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpGet("register")]
    [OutputCache(NoStore = true)]
    public IActionResult Register()
    {
        SetAuthMeta("register", "Create an account");
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        SetAuthMeta("register", "Create an account");
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Self-registration is limited to Guest and Partner — never Owner.
        var role = string.Equals(model.AccountType, UserRoles.Partner, StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Partner
            : UserRoles.Guest;

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PreferredLanguage = CurrentCulture,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await userManager.AddToRoleAsync(user, role);
        await audit.WriteAsync("account.register", user.Id, Ip, details: new { model.Email, Role = role });

        if (role == UserRoles.Partner)
        {
            // Partner accounts must confirm their email before they can sign in.
            await SendEmailConfirmationAsync(user);
            SetAuthMeta("register", "Check your email");
            return View("CheckEmail", "We've sent a confirmation link to activate your account.");
        }

        // Guests are signed in immediately.
        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect($"/{CurrentSlug}/my/bookings");
    }

    [HttpGet("confirm-email")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        SetAuthMeta("confirm-email", "Email confirmation");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return View("Info", "This confirmation link is invalid.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View("Info", "This confirmation link is invalid.");
        }

        var decoded = DecodeToken(token);
        var result = await userManager.ConfirmEmailAsync(user, decoded);
        await audit.WriteAsync("account.email_confirmed", user.Id, Ip, details: new { Success = result.Succeeded });

        return View("Info", result.Succeeded
            ? "Thanks — your email is confirmed. You can now sign in."
            : "We couldn't confirm your email. The link may have expired.");
    }

    [HttpGet("forgot-password")]
    [OutputCache(NoStore = true)]
    public IActionResult ForgotPassword()
    {
        SetAuthMeta("forgot-password", "Reset your password");
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        SetAuthMeta("forgot-password", "Reset your password");
        if (ModelState.IsValid)
        {
            var user = await userManager.FindByEmailAsync(model.Email);
            if (user is not null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var url = BuildAccountUrl("reset-password", new()
                {
                    ["email"] = model.Email,
                    ["token"] = EncodeToken(token),
                });
                await authEmail.SendAsync(
                    AuthEmailKind.PasswordReset, user.Email!, UserName(user), user.PreferredLanguage, url,
                    cancellationToken: HttpContext.RequestAborted);
                await audit.WriteAsync("account.password_reset_requested", user.Id, Ip);
            }
        }

        // Identical response whether or not the account exists (no enumeration).
        SetAuthMeta("forgot-password", "Check your email");
        return View("CheckEmail", "If that email has an account, we've sent a password reset link.");
    }

    [HttpGet("reset-password")]
    [OutputCache(NoStore = true)]
    public IActionResult ResetPassword(string? email = null, string? token = null)
    {
        SetAuthMeta("reset-password", "Choose a new password");
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty, Token = token ?? string.Empty });
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        SetAuthMeta("reset-password", "Choose a new password");
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            var result = await userManager.ResetPasswordAsync(user, DecodeToken(model.Token), model.Password);
            if (result.Succeeded)
            {
                await audit.WriteAsync("account.password_reset", user.Id, Ip);
                return View("Info", "Your password has been reset. You can now sign in.");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // Don't reveal that the account doesn't exist.
        return View("Info", "Your password has been reset. You can now sign in.");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Logout()
    {
        Guid? actorId = Guid.TryParse(userManager.GetUserId(User), out var id) ? id : null;
        await signInManager.SignOutAsync();
        await audit.WriteAsync("logout", actorId, Ip);
        return LocalRedirect($"/{CurrentSlug}");
    }

    [HttpGet("denied")]
    [OutputCache(NoStore = true)]
    public IActionResult Denied()
    {
        SetAuthMeta("denied", "Access denied");
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("Info", "You don't have access to that page.");
    }

    [HttpGet("magic-link")]
    [OutputCache(NoStore = true)]
    public IActionResult MagicLink()
    {
        SetAuthMeta("magic-link", "Access your booking");
        return View(new MagicLinkViewModel());
    }

    [HttpPost("magic-link")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> MagicLink(MagicLinkViewModel model)
    {
        SetAuthMeta("magic-link", "Access your booking");
        if (ModelState.IsValid)
        {
            var token = await magicLinks.IssueAsync(model.Reference, model.Email, HttpContext.RequestAborted);
            if (token is not null)
            {
                var url = BuildAccountUrl("magic-link/consume", new() { ["token"] = token });
                await authEmail.SendAsync(
                    AuthEmailKind.MagicLink, model.Email, model.Email, CurrentCulture, url,
                    reference: model.Reference.Trim().ToUpperInvariant(), cancellationToken: HttpContext.RequestAborted);
                await audit.WriteAsync("magic_link.issued", null, Ip, details: new { model.Reference });
            }
        }

        // Identical response whether or not the reference + email matched (no enumeration).
        SetAuthMeta("magic-link", "Check your email");
        return View("CheckEmail", "If that reference and email match a booking, we've sent an access link.");
    }

    [HttpGet("magic-link/consume")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> ConsumeMagicLink(string token)
    {
        var reference = await magicLinks.ConsumeAsync(token, HttpContext.RequestAborted);
        if (reference is null)
        {
            SetAuthMeta("magic-link", "Link expired");
            return View("Info", "This access link is invalid or has already been used. Please request a new one.");
        }

        bookingAccess.Grant(Response, reference);
        await audit.WriteAsync("magic_link.consumed", null, Ip, details: new { reference });
        return LocalRedirect($"/{CurrentSlug}/my/booking/{reference}");
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var url = BuildAccountUrl("confirm-email", new()
        {
            ["userId"] = user.Id.ToString(),
            ["token"] = EncodeToken(token),
        });
        await authEmail.SendAsync(
            AuthEmailKind.EmailConfirmation, user.Email!, UserName(user), user.PreferredLanguage, url,
            cancellationToken: HttpContext.RequestAborted);
    }

    private void SetAuthMeta(string path, string title) =>
        SetMeta(Meta($"account/{path}", $"{title} — {BrandName}", "Gaia Skyline account.", noIndex: true));

    private LocalRedirectResult RedirectToLocalOrDefault(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect($"/{CurrentSlug}/my/bookings");

    private string BuildAccountUrl(string path, Dictionary<string, string> query)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}/{CurrentSlug}/account/{path}";
        return QueryHelpers.AddQueryString(baseUrl, query!);
    }

    private static string UserName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.Email) ? "there" : user.Email!.Split('@')[0];

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
