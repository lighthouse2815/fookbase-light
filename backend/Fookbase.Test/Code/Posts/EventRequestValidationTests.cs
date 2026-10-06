using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Fookbase.Api.Modules.Posts.Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class EventRequestValidationTests(PostsApiFactory factory) : IClassFixture<PostsApiFactory>
{
    [Theory]
    [InlineData("hostType", "unknown", "HostType")]
    [InlineData("hostType", "99", "HostType")]
    [InlineData("hostId", "00000000-0000-0000-0000-000000000000", "HostId")]
    [InlineData("name", "   ", "Name")]
    [InlineData("privacy", "friends", "Privacy")]
    [InlineData("locationType", "unknown", "LocationType")]
    [InlineData("status", "cancelled", "Status")]
    [InlineData("status", "unknown", "Status")]
    [InlineData("startsAtUtc", "0001-01-01T00:00:00+00:00", "StartsAtUtc")]
    [InlineData("endsAtUtc", "2027-01-01T00:00:00+00:00", "EndsAtUtc")]
    [InlineData("coverMediaId", "00000000-0000-0000-0000-000000000000", "CoverMediaId")]
    public async Task Invalid_create_fields_are_rejected_with_field_errors(string field, string value, string member)
    {
        using var client = CreateClient();
        var body = EventBody();
        body[field] = value;
        await AssertValidationAsync(await client.PostAsJsonAsync("/api/events", body), member);
    }

    [Theory]
    [InlineData("name", 161, "Name")]
    [InlineData("description", 10_001, "Description")]
    public async Task Overlong_event_text_is_rejected_on_the_request(string field, int length, string member)
    {
        using var client = CreateClient();
        var body = EventBody();
        body[field] = new string('a', length);
        await AssertValidationAsync(await client.PostAsJsonAsync("/api/events", body), member);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("page")]
    public async Task Group_and_page_hosts_require_a_host_id(string hostType)
    {
        using var client = CreateClient();
        var body = EventBody();
        body["hostType"] = hostType;
        await AssertValidationAsync(await client.PostAsJsonAsync("/api/events", body), "HostId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    public async Task Online_events_require_an_http_url(string? url)
    {
        using var client = CreateClient();
        var body = EventBody();
        body["locationType"] = "online";
        body["onlineUrl"] = url;
        await AssertValidationAsync(await client.PostAsJsonAsync("/api/events", body), "OnlineUrl");
    }

    [Theory]
    [InlineData("privacy", "friends", "Privacy")]
    [InlineData("locationType", "unknown", "LocationType")]
    [InlineData("endsAtUtc", "2027-01-01T00:00:00+00:00", "EndsAtUtc")]
    public async Task Invalid_update_is_rejected_before_loading_the_event(string field, string value, string member)
    {
        using var client = CreateClient();
        var body = EventBody();
        body[field] = value;
        await AssertValidationAsync(await client.PatchAsJsonAsync($"/api/events/{Guid.NewGuid()}", body), member);
    }

    [Theory]
    [InlineData("going", false)]
    [InlineData("interested", false)]
    [InlineData("GOING", false)]
    [InlineData("unknown", true)]
    [InlineData("99", true)]
    [InlineData("", true)]
    public async Task Rsvp_validates_status_before_loading_the_event(string status, bool invalid)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync($"/api/events/{Guid.NewGuid()}/rsvp", new { status });
        if (invalid)
        {
            await AssertValidationAsync(response, "Status");
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Invitations_reject_an_empty_user_id_before_loading_the_event()
    {
        using var client = CreateClient();
        await AssertValidationAsync(await client.PostAsJsonAsync($"/api/events/{Guid.NewGuid()}/invites",
            new { userId = Guid.Empty }), "UserId");
    }

    [Theory]
    [InlineData("mine")]
    [InlineData("upcoming")]
    [InlineData("discover")]
    [InlineData("invitations/mine")]
    [InlineData("00000000-0000-0000-0000-000000000001/participants")]
    [InlineData("00000000-0000-0000-0000-000000000001/posts")]
    public async Task All_event_lists_validate_page_size_before_querying(string path)
    {
        using var client = CreateClient();
        foreach (var limit in new[] { 0, 101 })
        {
            await AssertValidationAsync(await client.GetAsync($"/api/events/{path}?limit={limit}"), "Limit");
        }
    }

    [Fact]
    public async Task Event_posts_validate_content_and_attachments_before_loading_the_event()
    {
        using var client = CreateClient();
        var path = $"/api/events/{Guid.NewGuid()}/posts";
        await AssertValidationAsync(await client.PostAsJsonAsync(path,
            new { content = "  ", mediaIds = Array.Empty<Guid>() }), "Content");
        await AssertValidationAsync(await client.PostAsJsonAsync(path,
            new { content = new string('a', 10_001), mediaIds = Array.Empty<Guid>() }), "Content");
        var mediaId = Guid.NewGuid();
        await AssertValidationAsync(await client.PostAsJsonAsync(path,
            new { content = "post", mediaIds = new[] { mediaId, mediaId } }), "MediaIds");
        await AssertValidationAsync(await client.PostAsJsonAsync(path,
            new { content = "post", mediaIds = new[] { Guid.Empty } }), "MediaIds");
        await AssertValidationAsync(await client.PostAsJsonAsync(path,
            new { content = "post", mediaIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray() }), "MediaIds");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path,
            new { content = "", mediaIds = new[] { mediaId } })).StatusCode);
    }

    [Fact]
    public async Task Cross_field_validation_keeps_errors_on_their_own_fields()
    {
        using var client = CreateClient();
        var body = EventBody();
        body["hostType"] = "group";
        body["locationType"] = "online";
        body["endsAtUtc"] = body["startsAtUtc"];
        using var response = await client.PostAsJsonAsync("/api/events", body);
        await AssertValidationAsync(response, "HostId");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal("Mã nhóm hoặc trang tổ chức sự kiện là bắt buộc.", errors.GetProperty("HostId")[0].GetString());
        Assert.Equal("Sự kiện trực tuyến cần URL http hoặc https hợp lệ.", errors.GetProperty("OnlineUrl")[0].GetString());
        Assert.Equal("Thời gian kết thúc phải sau thời gian bắt đầu.", errors.GetProperty("EndsAtUtc")[0].GetString());
    }

    [Fact]
    public async Task Event_post_attachment_limit_comes_from_configuration()
    {
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PostsOptions>();
                services.AddSingleton(new PostsOptions { MaximumAttachments = 2 });
            }));
        using var client = CreateClient(configuredFactory);
        Assert.Equal(2, configuredFactory.Services.GetRequiredService<PostsOptions>().MaximumAttachments);
        var path = $"/api/events/{Guid.NewGuid()}/posts";
        var mediaIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        using var response = await client.PostAsJsonAsync(path, new { mediaIds });
        await AssertValidationAsync(response, "MediaIds");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Bài viết chỉ được có tối đa 2 tệp đính kèm.",
            document.RootElement.GetProperty("errors").GetProperty("MediaIds")[0].GetString());
        using var accepted = await client.PostAsJsonAsync(path, new { mediaIds = mediaIds.Take(2).ToArray() });
        Assert.Equal(HttpStatusCode.NotFound, accepted.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Physical_event_updates_preserve_trimmed_limits_optional_ids_and_time_offsets(string? coverMediaId)
    {
        using var client = CreateClient();
        var body = EventBody();
        body["name"] = " " + new string('a', 160) + " ";
        body["description"] = " " + new string('b', 10_000) + " ";
        body["onlineUrl"] = "ignored-for-physical-events";
        body["startsAtUtc"] = "2027-01-01T07:00:00+07:00";
        body["endsAtUtc"] = "2027-01-01T01:00:00+00:00";
        body["coverMediaId"] = coverMediaId;
        using var response = await client.PatchAsJsonAsync($"/api/events/{Guid.NewGuid()}", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Dictionary<string, object?> EventBody() => new()
    {
        ["hostType"] = "user",
        ["name"] = "Event validation",
        ["privacy"] = "public",
        ["locationType"] = "physical",
        ["startsAtUtc"] = "2027-01-01T00:00:00+00:00",
        ["status"] = "published"
    };

    private HttpClient CreateClient() => CreateClient(factory);

    private static HttpClient CreateClient(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())], now.AddSeconds(-1), now.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string member)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(member, out _),
            await response.Content.ReadAsStringAsync());
    }
}
