using System.Net;
using System.Net.Mail;
using Fookbase.Api.Modules.Identity.Config;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public bool IsEnabled => options.Enabled;

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("SMTP email delivery is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseSsl,
            Credentials = new NetworkCredential(options.Username, options.Password)
        };
        await client.SendMailAsync(message);
    }
}
