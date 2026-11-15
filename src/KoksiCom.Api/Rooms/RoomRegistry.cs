using KoksiCom.Api.Guest;

namespace KoksiCom.Api.Rooms;

public sealed class RoomRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LiveRoom> _rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _membership = new(StringComparer.Ordinal);

    public IReadOnlyList<RoomListItem> List()
    {
        lock (_gate)
        {
            return _rooms.Select(pair => new RoomListItem(pair.Key, pair.Value.Members.Count)).ToArray();
        }
    }

    public string? RoomForGuest(string guest)
    {
        lock (_gate)
        {
            return _membership.GetValueOrDefault(guest);
        }
    }

    public IReadOnlyList<string> MembersInRoom(string roomName)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomName, out var room)
                ? room.Members.ToArray()
                : [];
        }
    }

    public CreateResult TryCreate(string guest, string? name, string? password)
    {
        if (!DisplayName.IsValid(name) || name is null || !RoomPassword.IsValid(password) || password is null)
        {
            return CreateResult.Invalid;
        }

        lock (_gate)
        {
            if (_membership.ContainsKey(guest))
            {
                return CreateResult.InRoom;
            }

            if (_rooms.ContainsKey(name))
            {
                return CreateResult.Taken;
            }

            var room = new LiveRoom(name, password);
            room.Members.Add(guest);
            _rooms[name] = room;
            _membership[guest] = name;
            return CreateResult.Created;
        }
    }

    public JoinResult TryJoin(string guest, string? name, string? password)
    {
        if (!DisplayName.IsValid(name) || name is null || !RoomPassword.IsValid(password) || password is null)
        {
            return JoinResult.Invalid;
        }

        lock (_gate)
        {
            if (_membership.ContainsKey(guest))
            {
                return JoinResult.InRoom;
            }

            if (!_rooms.TryGetValue(name, out var room))
            {
                return JoinResult.NotFound;
            }

            if (!string.Equals(room.Password, password, StringComparison.Ordinal))
            {
                return JoinResult.Forbidden;
            }

            room.Members.Add(guest);
            _membership[guest] = name;
            return JoinResult.Joined;
        }
    }

    public LeaveResult TryLeave(string guest)
    {
        lock (_gate)
        {
            if (!_membership.Remove(guest, out var roomName))
            {
                return LeaveResult.NotInRoom;
            }

            if (_rooms.TryGetValue(roomName, out var room))
            {
                room.Members.Remove(guest);
                if (room.Members.Count == 0)
                {
                    _rooms.Remove(roomName);
                }
            }

            return LeaveResult.Left;
        }
    }

    private sealed class LiveRoom(string name, string password)
    {
        public string Name { get; } = name;
        public string Password { get; } = password;
        public HashSet<string> Members { get; } = new(StringComparer.Ordinal);
    }
}

public enum CreateResult
{
    Created,
    Invalid,
    Taken,
    InRoom
}

public enum JoinResult
{
    Joined,
    Invalid,
    InRoom,
    NotFound,
    Forbidden
}

public enum LeaveResult
{
    Left,
    NotInRoom
}

public sealed record RoomListItem(string Name, int Users);
