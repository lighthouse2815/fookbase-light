namespace Fookbase.Api.Modules.Identity.Services;

public interface IEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
