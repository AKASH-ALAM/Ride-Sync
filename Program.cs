using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Middleware;
using RideSync.Web.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(cfg.GetConnectionString("Default")));

// ONE Redis multiplexer for the whole app: it is thread-safe and pipelines every command over a few sockets.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cfg.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IDistributedLock, RedisLock>();
builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
builder.Services.AddSingleton<RedisCache>();
builder.Services.AddSingleton<Idempotency>();
builder.Services.AddSingleton<DriverPresence>();
builder.Services.AddSingleton<IRideNotifier, SignalRRideNotifier>();
// SignalR with a Redis backplane: events published on one web server reach clients connected to any other server.
builder.Services.AddSignalR().AddStackExchangeRedis(cfg.GetConnectionString("Redis")!, o => o.Configuration.ChannelPrefix = RedisChannel.Literal("RideSync"));
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<RideService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddSingleton<LabService>();

builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.Cookie.HttpOnly = true;
    // A cookie can outlive its user (account deleted, database reset). Check the user still exists on every request;
    // if not, reject the cookie so the visitor is sent to the login page instead of hitting a crash deeper in the app.
    o.Events.OnValidatePrincipal = async ctx =>
    {
        var id = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDb>();
        if (!Guid.TryParse(id, out var uid) || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == uid))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync("Cookies");
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<RateLimitMiddleware>();   // after auth (knows the user), before MVC (cheap early rejection)
app.UseAuthorization();
app.MapHub<RideHub>("/hubs/ride");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
