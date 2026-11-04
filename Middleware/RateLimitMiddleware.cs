using System.Security.Claims;
using RideSync.Web.Services;

namespace RideSync.Web.Middleware;

/// Put on a controller/action to pick a named policy (see "RateLimits" in appsettings.json).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RateLimitAttribute(string policy) : Attribute { public string Policy { get; } = policy; }

public class RateLimitPolicy { public int Limit { get; set; } = 100; public int WindowSeconds { get; set; } = 60; }

public class RateLimitMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext ctx, IRateLimiter limiter)
    {
        var policyName = ctx.GetEndpoint()?.Metadata.GetMetadata<RateLimitAttribute>()?.Policy ?? "default";
        var policy = config.GetSection($"RateLimits:{policyName}").Get<RateLimitPolicy>() ?? new RateLimitPolicy();

        // Authenticated users are limited per user; anonymous callers (login/register) per IP.
        // JWT auth has already run, so User is populated when a token was sent.
        var who = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid
            ? $"user:{uid}" : $"ip:{ctx.Connection.RemoteIpAddress}";

        var r = await limiter.CheckAsync($"{policyName}:{who}", policy.Limit, TimeSpan.FromSeconds(policy.WindowSeconds));

        ctx.Response.Headers["X-RateLimit-Limit"] = r.Limit.ToString();
        ctx.Response.Headers["X-RateLimit-Remaining"] = r.Remaining.ToString();

        if (!r.Allowed)
        {
            var retry = Math.Max(1, (int)Math.Ceiling(r.RetryAfter.TotalSeconds));
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ctx.Response.Headers.RetryAfter = retry.ToString();
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync($"<h2>429 – Too many requests</h2><p>Slow down and retry in {retry}s.</p><a href=\"/\">Home</a>");
            return;
        }
        await next(ctx);
    }
}
