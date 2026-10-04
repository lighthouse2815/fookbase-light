using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.Services;

public interface IContactOtpSender
{
    Task SendAsync(
        ContactIdentifier contact,
        string code,
        CancellationToken cancellationToken = default);
}
