using System.Collections.Concurrent;

namespace KoksiCom.Api.Guest;

public sealed class GuestRegistry
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _guests = new(StringComparer.Ordinal);

    public bool TryAdd(string name, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        while (true)
        {
            if (_guests.TryGetValue(name, out var existing))
            {
                if (existing > now)
                {
                    return false;
                }

                if (_guests.TryUpdate(name, expiresAt, existing))
                {
                    return true;
                }

                continue;
            }

            if (_guests.TryAdd(name, expiresAt))
            {
                return true;
            }
        }
    }

    public bool ContainsActive(string name, DateTimeOffset now)
    {
        return _guests.TryGetValue(name, out var expiresAt) && expiresAt > now;
    }

    public bool Remove(string name)
    {
        return _guests.TryRemove(name, out _);
    }
}
