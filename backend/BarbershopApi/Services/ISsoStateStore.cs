using System.Collections.Concurrent;

namespace BarbershopApi.Services;

public interface ISsoStateStore
{
    bool TryConsume(string state);
}

public class InMemorySsoStateStore : ISsoStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _consumed = new();

    public bool TryConsume(string state)
    {
        var now = DateTimeOffset.UtcNow;
        CleanupExpired(now);

        return _consumed.TryAdd(state, now.Add(Ttl));
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        foreach (var (key, expiresAt) in _consumed)
        {
            if (expiresAt <= now)
            {
                _consumed.TryRemove(key, out _);
            }
        }
    }
}
