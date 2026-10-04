using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fookbase.Api.Modules.Games;
using Fookbase.Api.Modules.Games.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fookbase.Games.Tests;

public sealed class GameRoomHubTests
{
    [Fact]
    public async Task Authenticated_players_join_start_share_scores_and_transfer_host_over_websockets()
    {
        await using var app = CreateApp();
        await app.StartAsync();
        var server = app.GetTestServer();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        using var host = await ConnectAsync(server, "/hubs/jumping", hostId, timeout.Token);
        using var guest = await ConnectAsync(server, "/hubs/jumping", guestId, timeout.Token);
        using var flappy = await ConnectAsync(server, "/hubs/flappy-bird", hostId, timeout.Token);

        var created = await host.InvokeAsync("CreateRoom", [], timeout.Token);
        var code = created.GetProperty("result").GetProperty("code").GetString()!;
        var joined = await guest.InvokeAsync("JoinRoom", [code.ToLowerInvariant()], timeout.Token);
        Assert.Equal(2, joined.GetProperty("result").GetProperty("playerCount").GetInt32());
        Assert.False(joined.GetProperty("result").GetProperty("isHost").GetBoolean());

        var wrongGame = await flappy.InvokeAsync("JoinRoom", [code], timeout.Token);
        Assert.Contains("Không tìm thấy phòng", wrongGame.GetProperty("error").GetString());
        var denied = await guest.InvokeAsync("StartRound", [], timeout.Token);
        Assert.Contains("Chỉ chủ phòng", denied.GetProperty("error").GetString());

        await host.InvokeAsync("StartRound", [], timeout.Token);
        var started = await guest.EventAsync("RoundStarted", timeout.Token);
        var round = started.GetProperty("arguments")[0];
        var roundId = round.GetProperty("id").GetGuid();
        var membership = await host.InvokeAsync("RejoinRoom", [], timeout.Token);
        Assert.Equal(roundId, membership.GetProperty("result").GetProperty("round").GetProperty("id").GetGuid());

        await host.InvokeAsync("UpdatePlayer", [new { roundId, height = 999, phase = "playing", score = 1 }], timeout.Token);
        await host.InvokeAsync("UpdatePlayer", [new { roundId = Guid.NewGuid(), height = 20, phase = "playing", score = 2 }], timeout.Token);
        await host.InvokeAsync("UpdatePlayer", [new { roundId, height = 100, phase = "playing", score = 7 }], timeout.Token);
        var updated = (await guest.EventAsync("PlayerUpdated", timeout.Token)).GetProperty("arguments")[0];
        Assert.Equal(100, updated.GetProperty("height").GetDouble());
        Assert.Equal(7, updated.GetProperty("score").GetInt32());
        Assert.Equal("player", updated.GetProperty("username").GetString());

        await host.InvokeAsync("LeaveRoom", [], timeout.Token);
        await guest.EventAsync("PlayerLeft", timeout.Token);
        var remaining = (await guest.EventAsync("RoomUpdated", timeout.Token)).GetProperty("arguments")[0];
        Assert.Equal(guestId, remaining.GetProperty("hostUserId").GetGuid());
        Assert.Equal(1, remaining.GetProperty("playerCount").GetInt32());
        var restarted = await guest.InvokeAsync("StartRound", [], timeout.Token);
        Assert.False(restarted.TryGetProperty("error", out _));
    }

    [Theory]
    [InlineData("/hubs/jumping")]
    [InlineData("/hubs/flappy-bird")]
    public async Task Anonymous_players_cannot_connect(string path)
    {
        await using var app = CreateApp();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsync($"{path}/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGamesInfrastructure();
        builder.Services.AddSignalR();
        builder.Services.AddAuthentication("games-test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("games-test", _ => { });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<JumpingHub>("/hubs/jumping");
        app.MapHub<FlappyBirdHub>("/hubs/flappy-bird");
        return app;
    }

    private static async Task<HubSocket> ConnectAsync(TestServer server, string path, Guid userId, CancellationToken token)
    {
        var client = server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers["X-Test-User"] = userId.ToString();
        var socket = new HubSocket(await client.ConnectAsync(new Uri($"ws://localhost{path}"), token));
        await socket.SendAsync(new { protocol = "json", version = 1 }, token);
        await socket.ReadAsync(token);
        return socket;
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Guid.TryParse(Request.Headers["X-Test-User"], out var userId)
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.UniqueName, "player")], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }

    private sealed class HubSocket(WebSocket socket) : IDisposable
    {
        private readonly Queue<JsonElement> messages = new();
        private string remainder = "";
        private int invocationId;

        public Task SendAsync(object value, CancellationToken token) => socket.SendAsync(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + '\u001e'), WebSocketMessageType.Text, true, token);

        public async Task<JsonElement> InvokeAsync(string target, object[] arguments, CancellationToken token)
        {
            var id = (++invocationId).ToString();
            await SendAsync(new { type = 1, invocationId = id, target, arguments }, token);
            while (true)
            {
                var message = await ReadAsync(token);
                if (message.TryGetProperty("invocationId", out var received) && received.GetString() == id) return message;
            }
        }

        public async Task<JsonElement> EventAsync(string target, CancellationToken token)
        {
            while (true)
            {
                var message = await ReadAsync(token);
                if (message.TryGetProperty("target", out var received) && received.GetString() == target) return message;
            }
        }

        public async Task<JsonElement> ReadAsync(CancellationToken token)
        {
            while (messages.Count == 0)
            {
                var buffer = new byte[8192];
                var received = await socket.ReceiveAsync(buffer, token);
                remainder += Encoding.UTF8.GetString(buffer, 0, received.Count);
                var parts = remainder.Split('\u001e');
                remainder = parts[^1];
                foreach (var part in parts[..^1])
                {
                    using var document = JsonDocument.Parse(part);
                    messages.Enqueue(document.RootElement.Clone());
                }
            }
            return messages.Dequeue();
        }

        public void Dispose() => socket.Dispose();
    }
}
