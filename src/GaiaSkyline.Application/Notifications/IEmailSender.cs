namespace GaiaSkyline.Application.Notifications;

/// <summary>A ready-to-send email (HTML).</summary>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);

/// <summary>
/// Transport for outbound email. Dev uses SMTP (smtp4dev); production uses SendGrid — same interface,
/// chosen by configuration in the composition root.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
