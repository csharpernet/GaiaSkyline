using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>
/// Composes and sends the authentication emails from the <c>email.auth.*</c> content blocks in the
/// recipient's language, with built-in English fallbacks so an email always renders. Transport errors
/// are logged and swallowed (auth flows must not 500 or leak account existence on a mail failure).
/// </summary>
internal sealed class AuthEmailService(
    IContentService content,
    IEmailSender emailSender,
    ILogger<AuthEmailService> logger) : IAuthEmailService
{
    public async Task SendAsync(
        AuthEmailKind kind,
        string toEmail,
        string toName,
        string language,
        string actionUrl,
        string? reference = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await content.GetSectionAsync("email", language, cancellationToken);
            var keyBase = $"email.auth.{KindKey(kind)}";
            var (defaultSubject, defaultBody) = Defaults[kind];

            var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{name}"] = string.IsNullOrWhiteSpace(toName) ? "there" : toName,
                ["{actionUrl}"] = actionUrl,
                ["{reference}"] = reference ?? string.Empty,
            };

            var subject = Apply(Resolve(payload, $"{keyBase}.subject", defaultSubject), replacements);
            var body = WrapInLayout(Apply(Resolve(payload, $"{keyBase}.body", defaultBody), replacements));

            await emailSender.SendAsync(
                new EmailMessage(toEmail, string.IsNullOrWhiteSpace(toName) ? toEmail : toName, subject, body),
                cancellationToken);
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException
            or System.Net.Sockets.SocketException
            or IOException
            or System.Net.Http.HttpRequestException
            or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to send {Kind} auth email.", kind);
        }
    }

    private static string Resolve(ContentPayload payload, string key, string fallback) =>
        payload.Items.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.Text)
            ? value.Text!
            : fallback;

    private static string Apply(string template, Dictionary<string, string> replacements)
    {
        foreach (var (token, value) in replacements)
        {
            template = template.Replace(token, value, StringComparison.Ordinal);
        }

        return template;
    }

    private static string KindKey(AuthEmailKind kind) => kind switch
    {
        AuthEmailKind.EmailConfirmation => "confirm_email",
        AuthEmailKind.PasswordReset => "password_reset",
        AuthEmailKind.SetPassword => "set_password",
        AuthEmailKind.MagicLink => "magic_link",
        _ => "confirm_email",
    };

    private static string WrapInLayout(string innerHtml) =>
        "<!doctype html><html><body style=\"font-family:Arial,Helvetica,sans-serif;color:#0F1417;\">" +
        "<div style=\"max-width:560px;margin:0 auto;padding:24px;\">" +
        "<h1 style=\"font-size:20px;color:#A04A28;\">Gaia Skyline</h1>" +
        innerHtml +
        "<hr style=\"border:none;border-top:1px solid #D9D2C5;margin:24px 0;\" />" +
        "<p style=\"font-size:12px;color:#2E4F60;\">Gaia Skyline Apartment · Vila Nova de Gaia · AL 175890/AL</p>" +
        "</div></body></html>";

    private static readonly Dictionary<AuthEmailKind, (string Subject, string Body)> Defaults = new()
    {
        [AuthEmailKind.EmailConfirmation] = (
            "Confirm your email for Gaia Skyline",
            "<p>Hi {name},</p><p>Please confirm your email to activate your account.</p>" +
            "<p><a href=\"{actionUrl}\">Confirm my email</a></p>"),
        [AuthEmailKind.PasswordReset] = (
            "Reset your Gaia Skyline password",
            "<p>Hi {name},</p><p>Reset your password with the link below (valid one hour).</p>" +
            "<p><a href=\"{actionUrl}\">Reset my password</a></p>"),
        [AuthEmailKind.SetPassword] = (
            "Set your password for Gaia Skyline",
            "<p>Hi {name},</p><p>Set a password to manage your booking online (valid one hour).</p>" +
            "<p><a href=\"{actionUrl}\">Set my password</a></p>"),
        [AuthEmailKind.MagicLink] = (
            "Your Gaia Skyline booking access link",
            "<p>Hi,</p><p>Your secure link to booking <strong>{reference}</strong> (valid 30 minutes, single use).</p>" +
            "<p><a href=\"{actionUrl}\">View my booking</a></p>"),
    };
}
