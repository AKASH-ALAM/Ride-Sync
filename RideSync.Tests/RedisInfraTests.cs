using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using RideSync.Web.Services;
using StackExchange.Redis;

namespace RideSync.Tests;

public class RedisInfraTests
{
    private static IConnectionMultiplexer Redis() => ConnectionMultiplexer.Connect("localhost:6379");

    [Fact]
    public async Task Distributed_lock_gives_mutual_exclusion()
    {
        var @lock = new RedisLock(Redis());
        var key = $"test-{Guid.NewGuid():N}";
        var counter = 0;                     // deliberately NOT thread-safe
        await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            await using var h = await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
            Assert.NotNull(h);
            var read = counter; await Task.Delay(2); counter = read + 1;   // classic lost update if two get in
        }));
        Assert.Equal(50, counter);
    }

    [Fact]
    public async Task Lock_times_out_when_held_and_frees_on_dispose()
    {
        var @lock = new RedisLock(Redis());
        var key = $"test-{Guid.NewGuid():N}";
        var first = await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        Assert.NotNull(first);
        Assert.Null(await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)));
        await first!.DisposeAsync();
        Assert.NotNull(await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero));
    }

    [Fact]
    public async Task Expired_lock_holder_cannot_release_the_new_owners_lock()
    {
        var @lock = new RedisLock(Redis());
        var key = $"test-{Guid.NewGuid():N}";
        var slow = await @lock.AcquireAsync(key, TimeSpan.FromMilliseconds(100), TimeSpan.Zero);
        await Task.Delay(200);                                              // slow holder's TTL expires
        var fresh = await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        Assert.NotNull(fresh);
        await slow!.DisposeAsync();                                         // late release must be a no-op (token mismatch)
        Assert.Null(await @lock.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero)); // still held by `fresh`
    }

    [Fact]
    public async Task Rate_limiter_admits_exactly_limit_under_concurrency()
    {
        var rl = new RedisRateLimiter(Redis(), NullLogger<RedisRateLimiter>.Instance);
        var key = $"test-{Guid.NewGuid():N}";
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => rl.CheckAsync(key, 10, TimeSpan.FromSeconds(30))));
        Assert.Equal(10, results.Count(r => r.Allowed));     // atomic Lua → never 11
    }

    [Fact]
    public async Task Rate_limiter_window_slides()
    {
        var rl = new RedisRateLimiter(Redis(), NullLogger<RedisRateLimiter>.Instance);
        var key = $"test-{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++) Assert.True((await rl.CheckAsync(key, 3, TimeSpan.FromMilliseconds(400))).Allowed);
        var blocked = await rl.CheckAsync(key, 3, TimeSpan.FromMilliseconds(400));
        Assert.False(blocked.Allowed); Assert.True(blocked.RetryAfter > TimeSpan.Zero);
        await Task.Delay(450);
        Assert.True((await rl.CheckAsync(key, 3, TimeSpan.FromMilliseconds(400))).Allowed);
    }
}

public class WebTests
{
    [Fact]
    public async Task Login_page_is_throttled_with_429_and_Retry_After()
    {
        using var f = new WebFactory(new() { ["RateLimits:auth:Limit"] = "5", ["RateLimits:auth:WindowSeconds"] = "60" });
        var c = f.CreateClient();
        var codes = new List<HttpStatusCode>(); HttpResponseMessage? last = null;
        for (var i = 0; i < 8; i++) { last = await c.GetAsync("/Account/Login"); codes.Add(last.StatusCode); }
        Assert.Equal(5, codes.Count(x => x == HttpStatusCode.OK));
        Assert.Equal(3, codes.Count(x => x == HttpStatusCode.TooManyRequests));
        Assert.True(last!.Headers.Contains("Retry-After"));
        Assert.Equal("0", last.Headers.GetValues("X-RateLimit-Remaining").Single());
    }

    [Fact]
    public async Task Pages_render_and_protected_pages_redirect_to_login()
    {
        using var f = new WebFactory();
        var c = f.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Lab")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Account/Register")).StatusCode);
        var r = await c.GetAsync("/Rides");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location!.ToString());
    }
}
