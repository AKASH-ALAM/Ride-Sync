using StackExchange.Redis;

namespace RideSync.Web.Services;

/// <summary>
/// Who is online and where — kept in Redis, not SQL, because it changes every few seconds and is disposable.
///   drivers:geo   GEO set   (GEOADD / GEOSEARCH → "online drivers within R km of the pickup")
///   drivers:seen  ZSET      member = driverId, score = last heartbeat (unix s) → a driver who stops sending heartbeats drops out after 30 s
/// </summary>
public class DriverPresence(IConnectionMultiplexer redis)
{
    private const string Geo = "drivers:geo", Seen = "drivers:seen";
    private const int TtlSeconds = 30;
    private static double Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public async Task UpdateAsync(Guid driverId, double lat, double lng)
    {
        var db = redis.GetDatabase();
        var id = driverId.ToString();
        await Task.WhenAll(
            db.GeoAddAsync(Geo, lng, lat, id),
            db.SortedSetAddAsync(Seen, id, Now));
    }

    public async Task GoOfflineAsync(Guid driverId)
    {
        var db = redis.GetDatabase(); var id = driverId.ToString();
        await Task.WhenAll(db.GeoRemoveAsync(Geo, id), db.SortedSetRemoveAsync(Seen, id));
    }

    public async Task<int> OnlineCountAsync() =>
        (int)await redis.GetDatabase().SortedSetLengthAsync(Seen, Now - TtlSeconds, double.PositiveInfinity);

    public async Task<bool> IsOnlineAsync(Guid driverId) =>
        await redis.GetDatabase().SortedSetScoreAsync(Seen, driverId.ToString()) is { } s && s >= Now - TtlSeconds;

    public async Task<List<Guid>> NearbyAsync(double lat, double lng, double radiusKm)
    {
        var db = redis.GetDatabase();
        var hits = await db.GeoSearchAsync(Geo, lng, lat, new GeoSearchCircle(radiusKm, GeoUnit.Kilometers), count: 50, order: Order.Ascending);
        var result = new List<Guid>();
        foreach (var h in hits)
            if (await db.SortedSetScoreAsync(Seen, h.Member) is { } s && s >= Now - TtlSeconds && Guid.TryParse(h.Member.ToString(), out var g))
                result.Add(g);
        return result;
    }
}
