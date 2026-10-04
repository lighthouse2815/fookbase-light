using Fookbase.Api.Modules.Games;
using Fookbase.Api.Modules.Games.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Games.Tests;

public sealed class GameRoomServiceTests
{
    private readonly Guid host = Guid.NewGuid();
    private readonly Guid guest = Guid.NewGuid();
    private readonly GameRoomService rooms = new(TimeProvider.System);

    [Fact]
    public void Players_share_a_room_and_only_the_host_can_start_a_round()
    {
        var created = rooms.CreateRoom(host, "host");
        var joined = rooms.JoinRoom(created.Code, guest, "guest");

        Assert.Matches("^[A-Z2-9]{6}$", created.Code);
        Assert.True(created.IsHost);
        Assert.False(joined.IsHost);
        Assert.Equal(2, joined.PlayerCount);
        Assert.Throws<GameRoomException>(() => rooms.StartRound(guest, "guest"));

        var before = DateTimeOffset.UtcNow;
        var started = rooms.StartRound(host, "host");
        Assert.NotNull(started.Round);
        Assert.InRange(started.Round.StartsAtUtc, before.AddSeconds(3), DateTimeOffset.UtcNow.AddSeconds(3));
        Assert.Equal(started.Round, rooms.GetMembership("guest", guest)!.Round);
    }

    [Fact]
    public void Updates_require_membership_and_the_current_round()
    {
        var created = rooms.CreateRoom(host, "host");
        rooms.JoinRoom(created.Code, guest, "guest");
        var first = rooms.StartRound(host, "host").Round!;
        Assert.Equal(created.Code, rooms.GetRoomCodeForPlayerUpdate("guest", first.Id));
        Assert.Null(rooms.GetRoomCodeForPlayerUpdate("outsider", first.Id));

        var next = rooms.StartRound(host, "host").Round!;
        Assert.NotEqual(first.Id, next.Id);
        Assert.Null(rooms.GetRoomCodeForPlayerUpdate("guest", first.Id));
        rooms.LeaveRoom("guest");
        Assert.Null(rooms.GetRoomCodeForPlayerUpdate("guest", next.Id));
    }

    [Fact]
    public void Departing_host_transfers_ownership_to_a_remaining_player()
    {
        var created = rooms.CreateRoom(host, "host");
        rooms.JoinRoom(created.Code, guest, "guest");
        var update = rooms.LeaveRoom("host");

        Assert.Equal(guest, update!.HostUserId);
        Assert.Equal(1, update.PlayerCount);
        Assert.True(rooms.GetMembership("guest", guest)!.IsHost);
        Assert.NotNull(rooms.StartRound(guest, "guest").Round);
    }

    [Fact]
    public void Host_keeps_ownership_while_another_of_their_connections_remains()
    {
        var created = rooms.CreateRoom(host, "host");
        rooms.JoinRoom(created.Code, guest, "guest");
        rooms.JoinRoom(created.Code, host, "host-tab");
        Assert.Equal(host, rooms.LeaveRoom("host")!.HostUserId);
    }

    [Fact]
    public void Empty_rooms_are_removed_and_connections_can_join_again()
    {
        var created = rooms.CreateRoom(host, "host");
        Assert.Throws<GameRoomException>(() => rooms.CreateRoom(host, "host"));
        Assert.Throws<GameRoomException>(() => rooms.JoinRoom(created.Code, host, "host"));
        Assert.Equal(0, rooms.LeaveRoom("host")!.PlayerCount);
        Assert.Null(rooms.LeaveRoom("host"));
        Assert.Null(rooms.GetMembership("host", host));
        Assert.Throws<GameRoomException>(() => rooms.JoinRoom(created.Code, guest, "guest"));
        Assert.True(rooms.CreateRoom(host, "host").IsHost);
    }

    [Fact]
    public void Flappy_bird_and_jumping_use_separate_room_stores()
    {
        using var services = new ServiceCollection().AddGamesInfrastructure().BuildServiceProvider();
        var flappy = services.GetRequiredKeyedService<GameRoomService>("flappy-bird");
        var jumping = services.GetRequiredKeyedService<GameRoomService>("jumping");
        var room = jumping.CreateRoom(host, "host");
        Assert.Throws<GameRoomException>(() => flappy.JoinRoom(room.Code, guest, "guest"));
        Assert.True(flappy.CreateRoom(host, "host").IsHost);
        Assert.Same(jumping, services.GetRequiredKeyedService<GameRoomService>("jumping"));
    }
}
