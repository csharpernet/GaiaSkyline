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

        var streams = new List<MemoryStream>();
        if (message.Attachments is not null)
        {
            foreach (var attachment in message.Attachments)
            {
                var stream = new MemoryStream(attachment.Content);
                streams.Add(stream);
                mail.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }
        }

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
