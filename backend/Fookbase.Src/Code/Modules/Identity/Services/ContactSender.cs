using System.Net;
using System.Net.Mail;
using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class ContactSender(
    IHttpClientFactory httpClientFactory,
    EmailOptions emailOptions,
    SmsOptions smsOptions,
    ILogger<ContactSender> logger) : IEmailSender, IContactOtpSender
{
    public bool IsEnabled => emailOptions.Enabled;

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
            From = new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(emailOptions.Host, emailOptions.Port)
        {
            EnableSsl = emailOptions.UseSsl,
            Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password)
        };
        await client.SendMailAsync(message);
    }

    public async Task SendAsync(
        ContactIdentifier contact,
        string code,
        CancellationToken cancellationToken = default)
    {
        switch (contact.Kind)
        {
            case ContactKind.EMAIL when IsEnabled:
                await SendAsync(
                    contact.Value,
                    "Fookbase verification code",
                    $"<p>Your Fookbase verification code is <strong>{code}</strong>.</p>",
                    cancellationToken);
                return;
            case ContactKind.EMAIL:
                throw new InvalidOperationException("Email delivery is not configured.");
            case ContactKind.PHONE:
                await SendSmsAsync(contact.Value, code, cancellationToken);
                return;
            default:
                throw new InvalidOperationException("The verification contact is unsupported.");
        }
    }

    private async Task SendSmsAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken)
    {
        if (!smsOptions.Enabled || smsOptions.Provider != IdentityModuleConstants.SmsProviders.Traccar)
        {
            throw new InvalidOperationException("SMS delivery is not configured.");
        }

        using var client = httpClientFactory.CreateClient(nameof(ContactSender));
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(new
            {
                to = phoneNumber,
                message = $"Ma xac nhan Fookbase cua ban la: {code}"
            })
        };
        request.Headers.TryAddWithoutValidation("Authorization", smsOptions.AccessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Traccar rejected OTP dispatch with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("The SMS provider could not deliver the verification code.");
        }
    }
}
