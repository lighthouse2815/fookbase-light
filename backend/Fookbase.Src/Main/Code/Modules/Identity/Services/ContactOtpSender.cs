namespace Fookbase.Api.Modules.Identity.Services;

public sealed class ContactOtpSender(
    IEmailSender emailSender,
    SpeedSmsSender speedSmsSender) : IContactOtpSender
{
    public Task SendAsync(
        ContactIdentifier contact,
        string code,
        CancellationToken cancellationToken = default) =>
        contact.Kind switch
        {
            ContactKind.Email when emailSender.IsEnabled => emailSender.SendAsync(
                contact.Value,
                "Fookbase verification code",
                $"<p>Your Fookbase verification code is <strong>{code}</strong>.</p>",
                cancellationToken),
            ContactKind.Email => throw new InvalidOperationException("Email delivery is not configured."),
            ContactKind.Phone => speedSmsSender.SendOtpAsync(contact.Value, code, cancellationToken),
            _ => throw new InvalidOperationException("The verification contact is unsupported.")
        };
}
