namespace Fookbase.Api.Modules.Media.Config;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; init; } = "localhost:9000";

    // The browser-facing endpoint used in presigned URLs. This can differ from
    // Endpoint when the API reaches MinIO over a private Docker network.
    public string? PublicEndpoint { get; init; }

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string BucketName { get; init; } = "fookbase-media";

    public bool Secure { get; init; }

    public bool BucketInitializationEnabled { get; init; } = true;

    public int BucketInitializationMaxAttempts { get; init; } = 5;

    public int BucketInitializationRetrySeconds { get; init; } = 2;

    public string PresignedUrlEndpoint => string.IsNullOrWhiteSpace(PublicEndpoint)
        ? Endpoint
        : PublicEndpoint;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("MinIO endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(AccessKey))
        {
            throw new InvalidOperationException("MinIO access key is required.");
        }

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("MinIO secret key is required.");
        }

        if (string.IsNullOrWhiteSpace(BucketName))
        {
            throw new InvalidOperationException("MinIO bucket name is required.");
        }

        if (BucketInitializationMaxAttempts <= 0 || BucketInitializationRetrySeconds <= 0)
        {
            throw new InvalidOperationException("MinIO bucket initialization retry settings must be positive.");
        }
    }
}
