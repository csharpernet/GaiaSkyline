using GaiaSkyline.Application.Notifications;

namespace GaiaSkyline.Web.Testing;

/// <summary>
/// An <see cref="IEmailSender"/> that captures messages in memory instead of sending them, so the
/// Playwright suite can read a magic link without an SMTP server. Registered only when the E2E seam is on.
/// </summary>
internal sealed class CapturingEmailSender(E2EEmailSink sink, TimeProvider clock) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        sink.Add(new CapturedEmail(message.ToAddress, message.Subject, message.HtmlBody, clock.GetUtcNow()));
        return Task.CompletedTask;
    }
}
