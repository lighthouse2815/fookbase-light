using Fookbase.Api.Modules.Identity.Config;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class ContactOtpSender(
    IEmailSender emailSender,
    SpeedSmsSender speedSmsSender,
    TraccarSmsSender traccarSmsSender,
    SmsOptions smsOptions) : IContactOtpSender
{
    public async Task<string> SendAsync(
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
                return code;
            case ContactKind.Email:
                throw new InvalidOperationException("Email delivery is not configured.");
            case ContactKind.Phone:
                return smsOptions.Provider switch
                {
                    IdentityModuleConstants.SmsProviders.SpeedSms => await speedSmsSender.SendOtpAsync(contact.Value, cancellationToken),
                    IdentityModuleConstants.SmsProviders.Traccar => await traccarSmsSender.SendOtpAsync(contact.Value, code, cancellationToken),
                    _ => throw new InvalidOperationException("SMS delivery is not configured.")
                };
            default:
                throw new InvalidOperationException("The verification contact is unsupported.");
        }
    }
}
