using System.Security.Cryptography;

namespace Fookbase.Api.Modules.Games.Services;

public sealed class GameRoomService(TimeProvider timeProvider)
{
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly object syncRoot = new();
    private readonly Dictionary<string, GameRoom> rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> roomCodeByConnectionId = new(StringComparer.Ordinal);

    public GameRoomMembership CreateRoom(Guid ownerUserId, string connectionId)
    {
        lock (syncRoot)
        {
            EnsureConnectionIsNotInRoom(connectionId);
            var code = CreateRoomCode();
            var room = new GameRoom(code, ownerUserId);
            room.Players.Add(connectionId, ownerUserId);
            rooms.Add(code, room);
            roomCodeByConnectionId.Add(connectionId, code);
            return ToMembership(room, ownerUserId);
        }
    }

    public GameRoomMembership JoinRoom(string roomCode, Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            EnsureConnectionIsNotInRoom(connectionId);
            if (!rooms.TryGetValue(roomCode, out var room))
            {
                throw new GameRoomException("Không tìm thấy phòng. Hãy kiểm tra lại mã phòng.");
            }

            room.Players.Add(connectionId, userId);
            roomCodeByConnectionId.Add(connectionId, roomCode);
            return ToMembership(room, userId);
        }
    }

    public GameRoomMembership? GetMembership(string connectionId, Guid userId)
    {
        lock (syncRoot)
        {
            return roomCodeByConnectionId.TryGetValue(connectionId, out var roomCode) &&
                rooms.TryGetValue(roomCode, out var room)
                ? ToMembership(room, userId)
                : null;
        }
    }

    public GameRoomUpdate? LeaveRoom(string connectionId)
    {
        lock (syncRoot)
        {
            if (!roomCodeByConnectionId.Remove(connectionId, out var roomCode) ||
                !rooms.TryGetValue(roomCode, out var room))
            {
                return null;
            }

            room.Players.Remove(connectionId);
            if (room.Players.Count == 0)
            {
                rooms.Remove(roomCode);
            }
            else if (!room.Players.ContainsValue(room.OwnerUserId))
            {
                room.OwnerUserId = room.Players.Values.First();
            }

            return new GameRoomUpdate(roomCode, room.Players.Count, room.OwnerUserId);
        }
    }

    public GameRoomMembership StartRound(Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            var room = GetRoomForConnection(connectionId);
            if (room.OwnerUserId != userId)
            {
                throw new GameRoomException("Chỉ chủ phòng mới có thể bắt đầu vòng chơi.");
            }

            room.Round = new GameRound(
                Guid.NewGuid(),
                RandomNumberGenerator.GetInt32(int.MaxValue),
                timeProvider.GetUtcNow().AddSeconds(3));
            return ToMembership(room, userId);
        }
    }

    public string? GetRoomCodeForPlayerUpdate(string connectionId, Guid roundId)
    {
        lock (syncRoot)
        {
            if (!roomCodeByConnectionId.TryGetValue(connectionId, out var roomCode) ||
                !rooms.TryGetValue(roomCode, out var room) || room.Round?.Id != roundId)
            {
                return null;
            }

            return roomCode;
        }
    }

    private GameRoom GetRoomForConnection(string connectionId)
    {
        if (!roomCodeByConnectionId.TryGetValue(connectionId, out var roomCode) ||
            !rooms.TryGetValue(roomCode, out var room))
        {
            throw new GameRoomException("Hãy tạo hoặc tham gia một phòng trước.");
        }

        return room;
    }

    private void EnsureConnectionIsNotInRoom(string connectionId)
    {
        if (roomCodeByConnectionId.ContainsKey(connectionId))
        {
            throw new GameRoomException("Bạn đang ở trong một phòng khác.");
        }
    }

    private string CreateRoomCode()
    {
        string code;
        do
        {
            var characters = new char[6];
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = RoomAlphabet[RandomNumberGenerator.GetInt32(RoomAlphabet.Length)];
            }

            code = new string(characters);
        } while (rooms.ContainsKey(code));

        return code;
    }

    private static GameRoomMembership ToMembership(GameRoom room, Guid userId) =>
        new(room.Code, room.OwnerUserId == userId, room.Players.Count, room.Round, room.OwnerUserId);

    private sealed class GameRoom(string code, Guid ownerUserId)
    {
        public string Code { get; } = code;
        public Guid OwnerUserId { get; set; } = ownerUserId;
        public Dictionary<string, Guid> Players { get; } = new(StringComparer.Ordinal);
        public GameRound? Round { get; set; }
    }
}

public sealed record GameRound(Guid Id, int Seed, DateTimeOffset StartsAtUtc);

public sealed record GameRoomMembership(
    string Code,
    bool IsHost,
    int PlayerCount,
    GameRound? Round,
    Guid HostUserId);

public sealed record GameRoomUpdate(string Code, int PlayerCount, Guid HostUserId);

public sealed class GameRoomException(string message) : Exception(message);
