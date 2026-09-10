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
using Fookbase.Api.Modules.Media.Data;
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
        Assert.Equal(MediaStatus.Deleted,
            (await verifyDb.MediaAssets.AsNoTracking().SingleAsync(x => x.Id == unreferenced)).Status);
        Assert.True(await verifyDb.ObjectDeletions.AnyAsync(x => x.MediaId == unreferenced));
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
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var key = db.MediaAssets.AsNoTracking().Single(x => x.Id == mediaId).ObjectKey;
        scope.ServiceProvider.GetRequiredService<InMemoryObjectStorage>().Put(key, bytes, "image/png");
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
