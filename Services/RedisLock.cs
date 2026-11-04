using StackExchange.Redis;

namespace RideSync.Web.Services;

public interface IDistributedLock
{
    /// Tries to acquire the lock, retrying until <paramref name="wait"/> elapses. Returns null on timeout.
    Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan ttl, TimeSpan wait, CancellationToken ct = default);
}

/// <summary>
/// Single-instance Redis lock (the "SET NX PX" pattern).
///  - Acquire: SET lock:{resource} {uniqueToken} NX PX {ttl}   -> atomic "create if absent" with auto-expiry,
///             so a crashed holder can never deadlock the system.
///  - Release: Lua script "delete only if value == my token"    -> a slow holder whose TTL expired can't
///             delete the lock that now belongs to somebody else.
/// </summary>
public class RedisLock(IConnectionMultiplexer redis) : IDistributedLock
{
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end
        """;

    public async Task<IAsyncDisposable?> AcquireAsync(string resource, TimeSpan ttl, TimeSpan wait, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = $"lock:{resource}";
        var token = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow + wait;
        var delay = 10;

        while (true)
        {
            if (await db.StringSetAsync(key, token, ttl, When.NotExists))
                return new Handle(db, key, token);

            if (DateTime.UtcNow >= deadline) return null;
            // jittered backoff avoids all waiters hammering Redis in lock-step
            await Task.Delay(delay + Random.Shared.Next(0, 10), ct);
            delay = Math.Min(delay * 2, 100);
        }
    }

    private sealed class Handle(IDatabase db, string key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await db.ScriptEvaluateAsync(ReleaseScript, [(RedisKey)key], [(RedisValue)token]);
    }
}
