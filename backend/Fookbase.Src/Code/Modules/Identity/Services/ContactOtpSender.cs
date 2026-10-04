using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Domain.Enums;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class ContactOtpSender(
    IEmailSender emailSender,
    TraccarSmsSender traccarSmsSender) : IContactOtpSender
{
    public async Task SendAsync(
        ContactIdentifier contact,
        string code,
        CancellationToken cancellationToken = default)
    {
        switch (contact.Kind)
        {
            case ContactKind.Email when emailSender.IsEnabled:
                await emailSender.SendAsync(
                    contact.Value,
                    "Fookbase verification code",
                    $"<p>Your Fookbase verification code is <strong>{code}</strong>.</p>",
                    cancellationToken);
                return;
            case ContactKind.Email:
                throw new InvalidOperationException("Email delivery is not configured.");
            case ContactKind.Phone:
                await traccarSmsSender.SendOtpAsync(contact.Value, code, cancellationToken);
                return;
            default:
                throw new InvalidOperationException("The verification contact is unsupported.");
        }
    }
}
