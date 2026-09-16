using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Config;
using Google.Apis.Auth.OAuth2;

namespace Fookbase.Api.Modules.Identity.Services;

public sealed class FirebasePhoneTokenVerifier : IFirebasePhoneTokenVerifier
{
    private readonly FirebaseAuthenticationOptions options;
    private readonly Lazy<FirebaseAuth?> firebaseAuth;

    public FirebasePhoneTokenVerifier(FirebaseAuthenticationOptions options)
    {
        this.options = options;
        firebaseAuth = new Lazy<FirebaseAuth?>(CreateFirebaseAuth, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<ApplicationResult<FirebasePhoneIdentity>> VerifyAsync(
        string? idToken,
        string expectedPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(expectedPhoneNumber))
        {
            return InvalidToken();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var auth = firebaseAuth.Value;
        if (auth is null)
        {
            return Unavailable();
        }

        try
        {
            var token = await auth.VerifyIdTokenAsync(idToken);
            if (!token.Claims.TryGetValue("phone_number", out var value) || value is not string phoneNumber ||
                !string.Equals(phoneNumber, expectedPhoneNumber, StringComparison.Ordinal))
            {
                return InvalidToken();
            }

            return ApplicationResult<FirebasePhoneIdentity>.Success(new FirebasePhoneIdentity(token.Uid, phoneNumber));
        }
        catch (FirebaseAuthException)
        {
            return InvalidToken();
        }
        catch (FirebaseException)
        {
            return InvalidToken();
        }
    }

    private FirebaseAuth? CreateFirebaseAuth()
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            return null;
        }

        var app = FirebaseApp.Create(
            new AppOptions
            {
                Credential = GoogleCredential.FromJson(options.ServiceAccountJson),
                ProjectId = options.ProjectId
            },
            $"fookbase-firebase-phone-{options.ProjectId}");
        return FirebaseAuth.GetAuth(app);
    }

    private static ApplicationResult<FirebasePhoneIdentity> InvalidToken() =>
        ApplicationResult<FirebasePhoneIdentity>.Failure(new ApplicationError(
            "invalid_firebase_phone_token",
            "The phone verification is invalid or expired.",
            ApplicationErrorType.Unauthorized));

    private static ApplicationResult<FirebasePhoneIdentity> Unavailable() =>
        ApplicationResult<FirebasePhoneIdentity>.Failure(new ApplicationError(
            "firebase_phone_unavailable",
            "Phone verification is temporarily unavailable.",
            ApplicationErrorType.Conflict));
}
