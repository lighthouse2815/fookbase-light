using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Media.Api.IntegrationTests;

public sealed class MediaEndpointsTests(MediaApiFactory factory) : IClassFixture<MediaApiFactory>
{
    private static readonly byte[] PngContent =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x01, 0x02, 0x03];

    [Fact]
    public async Task Upload_without_jwt_returns_unauthorized()
    {
        using var client = factory.CreateClient();
        using var form = CreateForm(PngContent, "image/png", "avatar");

        var response = await client.PostAsync("/api/media", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_upload_download_and_delete_image()
    {
        var ownerUserId = Guid.NewGuid();
        using var client = CreateAuthenticatedClient(ownerUserId);
        using var form = CreateForm(PngContent, "image/png", "avatar");

        var uploadResponse = await client.PostAsync("/api/media", form);
        var uploaded = await ReadAsync<MediaResponse>(uploadResponse);
        var downloadResponse = await client.GetAsync(uploaded.Url);
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        var deleteResponse = await client.DeleteAsync(uploaded.Url);
        var missingResponse = await client.GetAsync(uploaded.Url);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Assert.Equal(ownerUserId, uploaded.OwnerUserId);
        Assert.Equal("avatar", uploaded.Purpose);
        Assert.Equal("image/png", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngContent, downloaded);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await dbContext.MediaAssets.AsNoTracking()
            .SingleAsync(item => item.Id == uploaded.Id);
        Assert.NotNull(asset.DeletedAt);
    }

    [Fact]
    public async Task Non_owner_cannot_delete_image()
    {
        using var owner = CreateAuthenticatedClient(Guid.NewGuid());
        using var other = CreateAuthenticatedClient(Guid.NewGuid());
        using var form = CreateForm(PngContent, "image/png", "post");
        var uploaded = await ReadAsync<MediaResponse>(
            await owner.PostAsync("/api/media", form));

        var response = await other.DeleteAsync(uploaded.Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(uploaded.Url)).StatusCode);
    }

    [Theory]
    [InlineData("text/plain", "post")]
    [InlineData("image/png", "unknown")]
    public async Task Upload_rejects_invalid_content_type_or_purpose(
        string contentType,
        string purpose)
    {
        using var client = CreateAuthenticatedClient(Guid.NewGuid());
        using var form = CreateForm(PngContent, contentType, purpose);

        var response = await client.PostAsync("/api/media", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent CreateForm(
        byte[] content,
        string contentType,
        string purpose)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "photo.png");
        form.Add(new StringContent(purpose), "purpose");
        return form;
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.AddSeconds(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("Response body was empty.");
    }
}
