using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed record FirebasePhoneIdentity(string Uid, string PhoneNumber);

public interface IFirebasePhoneTokenVerifier
{
    Task<ApplicationResult<FirebasePhoneIdentity>> VerifyAsync(
        string? idToken,
        string expectedPhoneNumber,
        CancellationToken cancellationToken = default);
}
