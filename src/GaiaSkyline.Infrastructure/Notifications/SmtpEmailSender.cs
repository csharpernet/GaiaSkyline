using System.Net.Mail;
using GaiaSkyline.Application.Notifications;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>Dev email transport over SMTP (smtp4dev on localhost:2525).</summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort);
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(new MailAddress(message.ToAddress, message.ToName));

        await client.SendMailAsync(mail, cancellationToken);
    }
}
