namespace Fookbase.Api.Modules.Identity.Services;

public interface IContactOtpSender
{
    Task<string> SendAsync(
        ContactIdentifier contact,
        string code,
        CancellationToken cancellationToken = default);
}
