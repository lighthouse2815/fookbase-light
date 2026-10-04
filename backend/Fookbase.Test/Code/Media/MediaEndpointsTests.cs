using Fookbase.Api.Modules.Media.DTOs.Requests;
using Fookbase.Api.Modules.Media.DTOs.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Api.Modules.Media.Services;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Media.Config;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Modules.Messages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using CloudinaryDotNet;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaEndpointsTests(MediaApiFactory factory) : IClassFixture<MediaApiFactory>
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01, 0x02, 0x03];
    private static readonly byte[] Mp4 =
        [0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m'];

    [Fact]
    public async Task Direct_upload_intent_uses_authenticated_cloudinary_image_endpoint()
    {
        var options = new CloudinaryOptions
        {
            CloudName = "test-cloud",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret"
        };
        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var storage = new CloudinaryObjectStorage(
            new Cloudinary(new Account(options.CloudName, options.ApiKey, options.ApiSecret)),
            options,
            services.GetRequiredService<IHttpClientFactory>());

        var intent = await storage.CreateDirectUploadIntentAsync(
            "user/avatar.png", MediaType.IMAGE, TimeSpan.FromMinutes(5));

        Assert.Equal("https://api.cloudinary.com/v1_1/test-cloud/image/upload", intent.UploadUrl);
        Assert.Equal("authenticated", intent.UploadParameters["type"]);
        Assert.Equal("false", intent.UploadParameters["overwrite"]);
        Assert.Equal(options.ApiKey, intent.UploadParameters["api_key"]);
        Assert.Matches("^[0-9a-f]{64}$", intent.UploadParameters["signature"]);
    }

    [Fact]
    public void Signed_download_url_preserves_extension_in_cloudinary_public_id()
    {
        var options = new CloudinaryOptions
        {
            CloudName = "test-cloud",
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret"
        };
        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var storage = new CloudinaryObjectStorage(
            new Cloudinary(new Account(options.CloudName, options.ApiKey, options.ApiSecret)),
            options,
            services.GetRequiredService<IHttpClientFactory>());

        var url = storage.CreateSignedGetUrl("user/avatar.png", MediaType.IMAGE);

        Assert.EndsWith("/user/avatar.png.png", url);
    }

    [Fact]
    public async Task Upload_intent_requires_jwt_and_validates_type_and_size()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("photo.png", "image/png", Png.Length))).StatusCode);

        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("file.txt", "text/plain", 10))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("large.png", "image/png", 20L * 1024 * 1024 + 1))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("photo.png", "image/png", Png.Length));
        var intent = await ReadAsync<UploadIntentResponse>(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(intent.MediaId.ToString("N"), intent.UploadUrl);
    }

    [Fact]
    public async Task Complete_rejects_missing_size_mismatch_and_invalid_signature()
    {
        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);

        var missing = await CreateIntentAsync(client, Png.Length);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/media/{missing.MediaId}/complete", null)).StatusCode);

        var oversized = await CreateIntentAsync(client, Png.Length);
        var oversizedBytes = new byte[20 * 1024 * 1024 + 1];
        Png.CopyTo(oversizedBytes, 0);
        PutObject(oversized.MediaId, oversizedBytes);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/media/{oversized.MediaId}/complete", null)).StatusCode);

        var invalid = await CreateIntentAsync(client, 8);
        PutObject(invalid.MediaId, Encoding.UTF8.GetBytes("not-a-png"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/media/{invalid.MediaId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Valid_completion_is_idempotent()
    {
        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);
        var intent = await CreateIntentAsync(client, Png.Length);
        PutObject(intent.MediaId, Png);

        var first = await client.PostAsync($"/api/media/{intent.MediaId}/complete", null);
        var second = await client.PostAsync($"/api/media/{intent.MediaId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

    }

    [Fact]
    public async Task Video_completion_queues_durable_processing_and_generates_private_derivatives()
    {
        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);
        var intent = await ReadAsync<UploadIntentResponse>(await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("reel.mp4", "video/mp4", Mp4.Length)));
        PutObject(intent.MediaId, Mp4, "video/mp4");

        var completed = await ReadAsync<MediaResponse>(
            await client.PostAsync($"/api/media/{intent.MediaId}/complete", null));
        Assert.Equal("Processing", completed.Status);
        var ready = await WaitForStatusAsync(client, intent.MediaId, "Ready");

        Assert.True(ready.HasProcessedVideo);
        Assert.Equal(10_000, ready.DurationMs);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var asset = await db.MediaAssets.AsNoTracking().SingleAsync(item => item.Id == intent.MediaId);
        Assert.True(await db.MediaProcessingJobs.AnyAsync(job => job.MediaId == intent.MediaId &&
            job.Status == MediaProcessingJobStatus.SUCCEEDED));
        var storage = scope.ServiceProvider.GetRequiredService<InMemoryObjectStorage>();
        Assert.True(storage.Contains(asset.ProcessedObjectKey!));
        Assert.True(storage.Contains(asset.PosterObjectKey!));
    }

    [Fact]
    public async Task Corrupt_video_processing_retries_then_fails_without_exposing_the_internal_error()
    {
        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);
        byte[] corrupt = [.. Mp4, 0xff];
        var intent = await ReadAsync<UploadIntentResponse>(await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("corrupt.mp4", "video/mp4", corrupt.Length)));
        PutObject(intent.MediaId, corrupt, "video/mp4");
        (await client.PostAsync($"/api/media/{intent.MediaId}/complete", null)).EnsureSuccessStatusCode();

        var failed = await WaitForStatusAsync(client, intent.MediaId, "Failed", TimeSpan.FromSeconds(10));
        Assert.False(failed.HasProcessedVideo);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var job = await db.MediaProcessingJobs.SingleAsync(item => item.MediaId == intent.MediaId);
        Assert.Equal(MediaProcessingJobStatus.FAILED, job.Status);
        Assert.Equal(3, job.AttemptCount);
        Assert.True(await db.ObjectDeletions.CountAsync(item => item.MediaId == intent.MediaId) >= 3);
    }

    [Fact]
    public async Task Deleting_an_unreferenced_processed_video_queues_original_and_derivative_cleanup()
    {
        var userId = CreateUserId();
        using var client = CreateAuthenticatedClient(userId);
        var mediaId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var asset = MediaAsset.CreatePending(mediaId, userId, MediaType.VIDEO,
            $"{userId:N}/{mediaId:N}.mp4", "reel.mp4", "video/mp4", 11, now, now.AddMinutes(5));
        asset.MarkProcessing(11, now);
        asset.MarkVideoReady(MediaAsset.ProcessedKey(userId, mediaId), MediaAsset.PosterKey(userId, mediaId),
            10_000, 720, 1280, now);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.MediaAssets.Add(asset);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/media/" + mediaId)).StatusCode);
        using var verification = factory.Services.CreateScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(3, await verificationDb.ObjectDeletions.CountAsync(item => item.MediaId == mediaId));
    }

    [Fact]
    public async Task Metadata_is_owner_only_and_internal_http_route_is_not_exposed()
    {
        var ownerId = CreateUserId();
        using var owner = CreateAuthenticatedClient(ownerId);
        using var other = CreateAuthenticatedClient(CreateUserId());
        var intent = await CreateIntentAsync(owner, Png.Length);
        PutObject(intent.MediaId, Png);
        await owner.PostAsync($"/api/media/{intent.MediaId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/media/{intent.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/media/{intent.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.PostAsync($"/internal/media/{intent.MediaId}/read-url", null)).StatusCode);
    }

    [Fact]
    public async Task Delete_is_blocked_by_reference_and_unreferenced_delete_queues_cleanup()
    {
        var ownerId = CreateUserId();
        using var owner = CreateAuthenticatedClient(ownerId);
        var referenced = await ReadyAsync(owner);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            db.MediaReferences.Add(MediaReference.Create(referenced, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.DeleteAsync($"/api/media/{referenced}")).StatusCode);

        var unreferenced = await ReadyAsync(owner);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/media/{unreferenced}")).StatusCode);
        using var verification = factory.Services.CreateScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Equal(MediaStatus.DELETED,
            (await verifyDb.MediaAssets.AsNoTracking().SingleAsync(x => x.Id == unreferenced)).Status);
        Assert.True(await verifyDb.ObjectDeletions.AnyAsync(x => x.MediaId == unreferenced));
    }

    [Fact]
    public async Task Delete_is_blocked_by_live_message_attachment_and_conversation_photo_until_released()
    {
        var ownerId = CreateUserId();
        using var owner = CreateAuthenticatedClient(ownerId);
        var attachmentMediaId = await ReadyAsync(owner);
        var photoMediaId = await ReadyAsync(owner);
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
            var message = new Message(Guid.NewGuid(), Guid.NewGuid(), ownerId, MessageType.MEDIA, null, null, now);
            var conversation = new Conversation(Guid.NewGuid(), "Photo reference", now);
            conversation.UpdateGroup("Photo reference", photoMediaId);
            db.Messages.Add(message);
            db.MessageAttachments.Add(new MessageAttachment(message.Id, attachmentMediaId, 0));
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();

            Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/media/{attachmentMediaId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/media/{photoMediaId}")).StatusCode);

            db.MessageAttachments.Remove((await db.MessageAttachments.SingleAsync(attachment => attachment.MessageId == message.Id)));
            conversation.UpdateGroup("Photo reference", null);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/media/{attachmentMediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/media/{photoMediaId}")).StatusCode);
    }

    private async Task<Guid> ReadyAsync(HttpClient owner)
    {
        var intent = await CreateIntentAsync(owner, Png.Length);
        PutObject(intent.MediaId, Png);
        (await owner.PostAsync($"/api/media/{intent.MediaId}/complete", null)).EnsureSuccessStatusCode();
        return intent.MediaId;
    }

    private static async Task<UploadIntentResponse> CreateIntentAsync(HttpClient client, long size) =>
        await ReadAsync<UploadIntentResponse>(await client.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("photo.png", "image/png", size)));

    private void PutObject(Guid mediaId, byte[] bytes, string contentType = "image/png")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var key = db.MediaAssets.AsNoTracking().Single(x => x.Id == mediaId).ObjectKey;
        scope.ServiceProvider.GetRequiredService<InMemoryObjectStorage>().Put(key, bytes, contentType);
    }

    private static async Task<MediaResponse> WaitForStatusAsync(
        HttpClient client,
        Guid mediaId,
        string expectedStatus,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/media/{mediaId}");
            var media = await ReadAsync<MediaResponse>(response);
            if (media.Status == expectedStatus)
            {
                return media;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Media {mediaId} did not reach {expectedStatus}.");
    }

    private static Guid CreateUserId() => Guid.NewGuid();

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], now.AddSeconds(-1), now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Empty response.");
    }
}
