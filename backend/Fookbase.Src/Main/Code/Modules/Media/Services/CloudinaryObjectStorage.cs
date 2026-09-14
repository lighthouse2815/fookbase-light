using System.Security.Cryptography;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Media.Entities;

namespace Fookbase.Api.Modules.Media.Services;

internal sealed class CloudinaryObjectStorage(
    Cloudinary cloudinary,
    CloudinaryOptions options,
    IHttpClientFactory httpClientFactory) : IObjectStorage
{
    public Task<DirectUploadIntent> CreateDirectUploadIntentAsync(string objectKey, MediaType mediaType,
        TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        var timestamp = expiresAt.AddHours(-1).ToUnixTimeSeconds().ToString();
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = objectKey,
            ["timestamp"] = timestamp,
            ["type"] = "authenticated",
            ["overwrite"] = "false"
        };
        var signatureSource = string.Join("&", fields.Select(item => $"{item.Key}={item.Value}"));
        var signature = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(signatureSource + options.ApiSecret))).ToLowerInvariant();
        fields["api_key"] = options.ApiKey;
        fields["signature"] = signature;
        var resourceType = mediaType == MediaType.Video ? "video" : "image";
        return Task.FromResult(new DirectUploadIntent(
            $"https://api.cloudinary.com/v1_1/{options.CloudName}/{resourceType}/upload", fields));
    }

    public string CreateSignedGetUrl(string objectKey, MediaType mediaType) =>
        (mediaType == MediaType.Video ? cloudinary.Api.UrlVideoUp : cloudinary.Api.UrlImgUp)
            .Secure(true).Type("authenticated").Signed(true).BuildUrl(objectKey);

    public Task<string> CreateSignedGetUrlAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default) => Task.FromResult(CreateSignedGetUrl(objectKey, mediaType));

    public async Task<StoredObjectInfo?> GetInfoAsync(string objectKey, MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        var result = await cloudinary.GetResourceAsync(new GetResourceParams(objectKey)
        {
            ResourceType = ToResourceType(mediaType), Type = "authenticated"
        }, cancellationToken);
        if (result.Error is not null) return null;
        return new StoredObjectInfo(result.Bytes, mediaType,
            string.Equals(result.Type, "authenticated", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<byte[]> ReadPrefixAsync(string objectKey, MediaType mediaType, int length,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, CreateSignedGetUrl(objectKey, mediaType));
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, length - 1);
        using var response = await httpClientFactory.CreateClient(nameof(CloudinaryObjectStorage))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.Length <= length ? bytes : bytes[..length];
    }

    public async Task DownloadToFileAsync(string objectKey, MediaType mediaType, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClientFactory.CreateClient(nameof(CloudinaryObjectStorage))
            .GetAsync(CreateSignedGetUrl(objectKey, mediaType), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadFileAsync(string objectKey, MediaType mediaType, string sourcePath, string contentType,
        CancellationToken cancellationToken = default)
    {
        if (mediaType == MediaType.Video)
            await cloudinary.UploadAsync(new VideoUploadParams { File = new FileDescription(sourcePath), PublicId = objectKey, Type = "authenticated", Overwrite = true }, cancellationToken);
        else
            await cloudinary.UploadAsync(new ImageUploadParams { File = new FileDescription(sourcePath), PublicId = objectKey, Type = "authenticated", Overwrite = true }, cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, MediaType mediaType, CancellationToken cancellationToken = default)
    {
        var result = await cloudinary.DestroyAsync(new DeletionParams(objectKey)
        {
            ResourceType = ToResourceType(mediaType), Type = "authenticated", Invalidate = true
        });
        if (result.Error is not null) throw new InvalidOperationException(result.Error.Message);
    }

    private static ResourceType ToResourceType(MediaType mediaType) =>
        mediaType == MediaType.Video ? ResourceType.Video : ResourceType.Image;
}
