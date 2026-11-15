using System.Text.Json.Serialization;

namespace KoksiCom.Api.Signaling;

public sealed class RoomPresence
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PeerInfo> _byName = new(StringComparer.Ordinal);

    public PeerInfo Attach(string name, string connectionId)
    {
        lock (_gate)
        {
            if (!_byName.TryGetValue(name, out var info))
            {
                info = new PeerInfo
                {
                    Name = name,
                    Mic = false,
                    Headphones = true,
                    Share = false
                };
                _byName[name] = info;
            }

            info.ConnectionId = connectionId;
            return info.Copy();
        }
    }

    public void Detach(string name)
    {
        lock (_gate)
        {
            _byName.Remove(name);
        }
    }

    public string? ConnectionId(string name)
    {
        lock (_gate)
        {
            return _byName.GetValueOrDefault(name)?.ConnectionId;
        }
    }

    public PeerInfo? UpdateState(string name, bool mic, bool headphones, bool share)
    {
        lock (_gate)
        {
            if (!_byName.TryGetValue(name, out var info))
            {
                return null;
            }

            info.Mic = mic;
            info.Headphones = headphones;
            info.Share = share;
            return info.Copy();
        }
    }

    public IReadOnlyList<PeerInfo> Roster(IEnumerable<string> names)
    {
        lock (_gate)
        {
            var list = new List<PeerInfo>();
            foreach (var name in names)
            {
                if (_byName.TryGetValue(name, out var info))
                {
                    list.Add(info.Copy());
                }
            }

            return list;
        }
    }
}

public sealed class PeerInfo
{
    public required string Name { get; init; }
    public bool Mic { get; set; }
    public bool Headphones { get; set; }
    public bool Share { get; set; }
    [JsonIgnore]
    public string? ConnectionId { get; set; }

    public PeerInfo Copy() => new()
    {
        Name = Name,
        Mic = Mic,
        Headphones = Headphones,
        Share = Share,
        ConnectionId = ConnectionId
    };
}
