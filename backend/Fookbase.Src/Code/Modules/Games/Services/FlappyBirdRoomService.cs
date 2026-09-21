using System.Security.Cryptography;

namespace Fookbase.Api.Modules.Games.Services;

public sealed class FlappyBirdRoomService(TimeProvider timeProvider)
{
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly object syncRoot = new();
    private readonly Dictionary<string, FlappyBirdRoom> rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> roomCodeByConnectionId = new(StringComparer.Ordinal);

    public FlappyBirdRoomMembership CreateRoom(Guid ownerUserId, string connectionId)
    {
        lock (syncRoot)
        {
            EnsureConnectionIsNotInRoom(connectionId);
            var code = CreateRoomCode();
            var room = new FlappyBirdRoom(code, ownerUserId);
            room.ConnectionIds.Add(connectionId);
            rooms.Add(code, room);
            roomCodeByConnectionId.Add(connectionId, code);
            return ToMembership(room, ownerUserId);
        }
    }

    public FlappyBirdRoomMembership JoinRoom(string roomCode, Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            EnsureConnectionIsNotInRoom(connectionId);
            if (!rooms.TryGetValue(roomCode, out var room))
            {
                throw new FlappyBirdRoomException("Không tìm thấy phòng. Hãy kiểm tra lại mã phòng.");
            }

            room.ConnectionIds.Add(connectionId);
            roomCodeByConnectionId.Add(connectionId, roomCode);
            return ToMembership(room, userId);
        }
    }

    public FlappyBirdRoomMembership? GetMembership(string connectionId, Guid userId)
    {
        lock (syncRoot)
        {
            return roomCodeByConnectionId.TryGetValue(connectionId, out var roomCode) &&
                rooms.TryGetValue(roomCode, out var room)
                ? ToMembership(room, userId)
                : null;
        }
    }

    public FlappyBirdRoomUpdate? LeaveRoom(string connectionId)
    {
        lock (syncRoot)
        {
            if (!roomCodeByConnectionId.Remove(connectionId, out var roomCode) ||
                !rooms.TryGetValue(roomCode, out var room))
            {
                return null;
            }

            room.ConnectionIds.Remove(connectionId);
            if (room.ConnectionIds.Count == 0)
            {
                rooms.Remove(roomCode);
            }

            return new FlappyBirdRoomUpdate(roomCode, room.ConnectionIds.Count);
        }
    }

    public FlappyBirdRoomMembership StartRound(Guid userId, string connectionId)
    {
        lock (syncRoot)
        {
            var room = GetRoomForConnection(connectionId);
            if (room.OwnerUserId != userId)
            {
                throw new FlappyBirdRoomException("Chỉ chủ phòng mới có thể bắt đầu vòng chơi.");
            }

            room.Round = new FlappyBirdRound(
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

    private FlappyBirdRoom GetRoomForConnection(string connectionId)
    {
        if (!roomCodeByConnectionId.TryGetValue(connectionId, out var roomCode) ||
            !rooms.TryGetValue(roomCode, out var room))
        {
            throw new FlappyBirdRoomException("Hãy tạo hoặc tham gia một phòng trước.");
        }

        return room;
    }

    private void EnsureConnectionIsNotInRoom(string connectionId)
    {
        if (roomCodeByConnectionId.ContainsKey(connectionId))
        {
            throw new FlappyBirdRoomException("Bạn đang ở trong một phòng khác.");
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

    private static FlappyBirdRoomMembership ToMembership(FlappyBirdRoom room, Guid userId) =>
        new(room.Code, room.OwnerUserId == userId, room.ConnectionIds.Count, room.Round);

    private sealed class FlappyBirdRoom(string code, Guid ownerUserId)
    {
        public string Code { get; } = code;
        public Guid OwnerUserId { get; } = ownerUserId;
        public HashSet<string> ConnectionIds { get; } = new(StringComparer.Ordinal);
        public FlappyBirdRound? Round { get; set; }
    }
}

public sealed record FlappyBirdRound(Guid Id, int Seed, DateTimeOffset StartsAtUtc);

public sealed record FlappyBirdRoomMembership(
    string Code,
    bool IsHost,
    int PlayerCount,
    FlappyBirdRound? Round);

public sealed record FlappyBirdRoomUpdate(string Code, int PlayerCount);

public sealed class FlappyBirdRoomException(string message) : Exception(message);
