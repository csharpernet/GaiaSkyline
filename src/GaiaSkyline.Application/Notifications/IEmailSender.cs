namespace GaiaSkyline.Application.Notifications;

/// <summary>A file attached to an email (e.g. a booking .ics).</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>A ready-to-send email (HTML), optionally with attachments.</summary>
public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>
/// Transport for outbound email. Dev uses SMTP (smtp4dev); production uses SendGrid — same interface,
/// chosen by configuration in the composition root.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
