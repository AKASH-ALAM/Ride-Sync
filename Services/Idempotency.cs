using StackExchange.Redis;

namespace RideSync.Web.Services;

/// <summary>
/// Makes "request a ride" safe to retry (double-tap, flaky mobile network). The client sends an
/// Idempotency-Key header; the first request claims it with SET NX (atomic), repeats get the stored result.
/// </summary>
public class Idempotency(IConnectionMultiplexer redis)
{
    /// Returns (true, null) if this caller owns the key; (false, value) if somebody already claimed it.
    public async Task<(bool Claimed, string? Existing)> TryClaimAsync(string key)
    {
        var db = redis.GetDatabase();
        if (await db.StringSetAsync($"idem:{key}", "pending", TimeSpan.FromHours(24), When.NotExists))
            return (true, null);
        return (false, (string?)await db.StringGetAsync($"idem:{key}"));
    }

    public Task CompleteAsync(string key, string value) =>
        redis.GetDatabase().StringSetAsync($"idem:{key}", value, TimeSpan.FromHours(24));

    public Task ReleaseAsync(string key) => redis.GetDatabase().KeyDeleteAsync($"idem:{key}");
}
