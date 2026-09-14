using Minio;

namespace Fookbase.Api.Modules.Media.Services;

internal sealed class MinioPresignedUrlClient(IMinioClient client)
{
    public IMinioClient Client { get; } = client;
}
