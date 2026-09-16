using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Services;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class TestFirebasePhoneTokenVerifier : IFirebasePhoneTokenVerifier
{
    private const string Prefix = "firebase-test-phone:";

    public static string TokenFor(string phoneNumber) => $"{Prefix}{phoneNumber}";

    public Task<ApplicationResult<FirebasePhoneIdentity>> VerifyAsync(
        string? idToken,
        string expectedPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var verifiedPhoneNumber = idToken?.StartsWith(Prefix, StringComparison.Ordinal) == true
            ? idToken[Prefix.Length..]
            : null;
        if (verifiedPhoneNumber is not null && string.Equals(verifiedPhoneNumber, expectedPhoneNumber, StringComparison.Ordinal))
        {
            return Task.FromResult(ApplicationResult<FirebasePhoneIdentity>.Success(
                new FirebasePhoneIdentity("firebase-test-user", verifiedPhoneNumber)));
        }

        return Task.FromResult(ApplicationResult<FirebasePhoneIdentity>.Failure(new ApplicationError(
            "invalid_firebase_phone_token",
            "The phone verification is invalid or expired.",
            ApplicationErrorType.Unauthorized)));
    }
}
