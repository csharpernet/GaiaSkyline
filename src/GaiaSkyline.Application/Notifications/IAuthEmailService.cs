namespace GaiaSkyline.Application.Notifications;

/// <summary>The authentication emails, keyed to the <c>email.auth.{kind}</c> content blocks.</summary>
public enum AuthEmailKind
{
    EmailConfirmation,
    PasswordReset,
    SetPassword,
    MagicLink,
}

/// <summary>
/// Sends an authentication email in the recipient's language. Resilient: a transport failure is logged
/// and swallowed so it never breaks (or leaks the outcome of) a login, register or reset flow.
/// </summary>
public interface IAuthEmailService
{
    Task SendAsync(
        AuthEmailKind kind,
        string toEmail,
        string toName,
        string language,
        string actionUrl,
        string? reference = null,
        CancellationToken cancellationToken = default);
}
