using GaiaSkyline.Application.Notifications;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>Production email transport over SendGrid (selected by Email:Provider configuration).</summary>
internal sealed class SendGridEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var client = new SendGridClient(_options.SendGridApiKey);
        var from = new EmailAddress(_options.FromAddress, _options.FromName);
        var to = new EmailAddress(message.ToAddress, message.ToName);
        var email = MailHelper.CreateSingleEmail(from, to, message.Subject, plainTextContent: null, message.HtmlBody);

        if (message.Attachments is not null)
        {
            foreach (var attachment in message.Attachments)
            {
                email.AddAttachment(attachment.FileName, Convert.ToBase64String(attachment.Content), attachment.ContentType);
            }
        }

        var response = await client.SendEmailAsync(email, cancellationToken);
        if ((int)response.StatusCode >= 400)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid rejected the email ({(int)response.StatusCode}): {body}");
        }
    }
}
