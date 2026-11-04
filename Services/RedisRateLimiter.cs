using StackExchange.Redis;

namespace RideSync.Web.Services;

public record RateLimitResult(bool Allowed, int Limit, int Remaining, TimeSpan RetryAfter);

public interface IRateLimiter
{
    Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window);
}

/// <summary>
/// Sliding-window-log rate limiter. Each request is a member of a sorted set scored by its timestamp.
/// The whole "trim old → count → maybe add" sequence runs inside ONE Lua script, which Redis executes
/// atomically — so two concurrent requests can never both read count=limit-1 and both get in
/// (that read-then-write gap would itself be a race condition).
/// Time comes from Redis (TIME), not the app server, so multiple API instances can't disagree on "now".
/// </summary>
public class RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> log) : IRateLimiter
{
    private const string Script = """
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        local window = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now - window)
        local count = redis.call('ZCARD', KEYS[1])
        if count < limit then
            redis.call('ZADD', KEYS[1], now, ARGV[3])
            redis.call('PEXPIRE', KEYS[1], window)
            return {1, limit - count - 1, 0}
        end
        local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
        return {0, 0, tonumber(oldest[2]) + window - now}
        """;

    public async Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window)
    {
        try
        {
            var res = (long[])(await redis.GetDatabase().ScriptEvaluateAsync(
                Script, [(RedisKey)$"rl:{key}"],
                [(RedisValue)(long)window.TotalMilliseconds, (RedisValue)limit, (RedisValue)Guid.NewGuid().ToString("N")]))!;
            return new RateLimitResult(res[0] == 1, limit, (int)res[1], TimeSpan.FromMilliseconds(Math.Max(res[2], 0)));
        }
        catch (RedisException ex)
        {
            // Fail OPEN: a Redis outage should degrade protection, not take the whole API down.
            log.LogError(ex, "Rate limiter unavailable, allowing request");
            return new RateLimitResult(true, limit, limit, TimeSpan.Zero);
        }
    }
}
