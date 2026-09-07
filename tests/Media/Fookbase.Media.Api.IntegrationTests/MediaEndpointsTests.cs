using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Contracts.Identity;
using Fookbase.Contracts.Media;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Domain.Entities;
using Fookbase.Media.Infrastructure.IntegrationEvents;
using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaEndpointsTests(MediaApiFactory factory) : IClassFixture<MediaApiFactory>
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01, 0x02, 0x03];

    [Fact]
    public async Task Upload_intent_requires_jwt_and_validates_type_and_size()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/media/uploads",
            new CreateUploadRequest("photo.png", "image/png", Png.Length))).StatusCode);

        var userId = await CreateKnownUserAsync();
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
        var userId = await CreateKnownUserAsync();
        using var client = CreateAuthenticatedClient(userId);

        var missing = await CreateIntentAsync(client, Png.Length);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/media/{missing.MediaId}/complete", null)).StatusCode);

        var oversized = await CreateIntentAsync(client, Png.Length);
        PutObject(oversized.MediaId, [.. Png, 0x04]);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/media/{oversized.MediaId}/complete", null)).StatusCode);

        var invalid = await CreateIntentAsync(client, 8);
        PutObject(invalid.MediaId, Encoding.UTF8.GetBytes("not-a-png"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync($"/api/media/{invalid.MediaId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Valid_completion_is_idempotent_and_writes_one_ready_outbox_event()
    {
        var userId = await CreateKnownUserAsync();
        using var client = CreateAuthenticatedClient(userId);
        var intent = await CreateIntentAsync(client, Png.Length);
        PutObject(intent.MediaId, Png);

        var first = await client.PostAsync($"/api/media/{intent.MediaId}/complete", null);
        var second = await client.PostAsync($"/api/media/{intent.MediaId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var readyEvents = await db.OutboxMessages.AsNoTracking()
            .Where(x => x.Type == MediaReadyIntegrationEvent.EventType).ToListAsync();
        Assert.Equal(1, readyEvents.Count(x => x.Payload.Contains(intent.MediaId.ToString())));
    }

    [Fact]
    public async Task Metadata_is_owner_only_and_internal_read_url_requires_service_token()
    {
        var ownerId = await CreateKnownUserAsync();
        using var owner = CreateAuthenticatedClient(ownerId);
        using var other = CreateAuthenticatedClient(await CreateKnownUserAsync());
        var intent = await CreateIntentAsync(owner, Png.Length);
        PutObject(intent.MediaId, Png);
        await owner.PostAsync($"/api/media/{intent.MediaId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/media/{intent.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/media/{intent.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await owner.PostAsync($"/internal/media/{intent.MediaId}/read-url", null)).StatusCode);
        owner.DefaultRequestHeaders.Add("X-Internal-Service-Token", "integration-tests-internal-token-32-chars");
        var readUrl = await ReadAsync<MediaReadUrlResponse>(
            await owner.PostAsync($"/internal/media/{intent.MediaId}/read-url", null));
        Assert.Contains("get=1", readUrl.Url);
    }

    [Fact]
    public async Task Delete_is_blocked_by_reference_and_unreferenced_delete_uses_outbox_and_cleanup_queue()
    {
        var ownerId = await CreateKnownUserAsync();
        using var owner = CreateAuthenticatedClient(ownerId);
        var referenced = await ReadyAsync(owner);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.MediaReferences.Add(MediaReference.Create(referenced, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.DeleteAsync($"/api/media/{referenced}")).StatusCode);

        var unreferenced = await ReadyAsync(owner);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.DeleteAsync($"/api/media/{unreferenced}")).StatusCode);
        using var verification = factory.Services.CreateScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(MediaStatus.Deleted,
            (await verifyDb.MediaAssets.AsNoTracking().SingleAsync(x => x.Id == unreferenced)).Status);
        var deletedEvents = await verifyDb.OutboxMessages.AsNoTracking()
            .Where(x => x.Type == MediaDeletedIntegrationEvent.EventType).ToListAsync();
        Assert.Contains(deletedEvents, x => x.Payload.Contains(unreferenced.ToString()));
        Assert.True(await verifyDb.ObjectDeletions.AnyAsync(x => x.MediaId == unreferenced));
    }

    [Fact]
    public async Task User_projection_inbox_is_idempotent()
    {
        var message = new UserRegisteredIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "media-user", DateTimeOffset.UtcNow);
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<MediaProjectionStore>();
        Assert.True(await store.ProjectAsync(message));
        Assert.False(await store.ProjectAsync(message));
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(1, await db.InboxMessages.CountAsync(x => x.EventId == message.EventId));
        Assert.Equal(1, await db.KnownUsers.CountAsync(x => x.UserId == message.UserId));
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

    private void PutObject(Guid mediaId, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var key = db.MediaAssets.AsNoTracking().Single(x => x.Id == mediaId).ObjectKey;
        scope.ServiceProvider.GetRequiredService<InMemoryObjectStorage>().Put(key, bytes, "image/png");
    }

    private async Task<Guid> CreateKnownUserAsync()
    {
        var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        db.KnownUsers.Add(KnownUser.Create(id, $"user-{id:N}"[..30], DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return id;
    }

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
