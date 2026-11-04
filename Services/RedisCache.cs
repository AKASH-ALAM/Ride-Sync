using System.Text.Json;
using StackExchange.Redis;

namespace RideSync.Web.Services;

/// Cache-aside helper. Redis errors never fail a request: we just fall through to the database.
public class RedisCache(IConnectionMultiplexer redis, ILogger<RedisCache> log)
{
    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key);
            if (cached.HasValue) return JsonSerializer.Deserialize<T>((string)cached!)!;
        }
        catch (RedisException ex) { log.LogWarning(ex, "Cache read failed"); }

        var value = await factory();
        try { await redis.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value), ttl); }
        catch (RedisException ex) { log.LogWarning(ex, "Cache write failed"); }
        return value;
    }

    public async Task InvalidateAsync(string key)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(key); }
        catch (RedisException ex) { log.LogWarning(ex, "Cache invalidate failed"); }
    }
}
