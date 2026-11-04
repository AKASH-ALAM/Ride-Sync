using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RideSync.Web.Data;
using RideSync.Web.Models;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // tests share one Redis DB → run one at a time

namespace RideSync.Tests;

/// Boots the real MVC app in-process against a throwaway SQL Server database and Redis DB #15 (flushed on start).
public class WebFactory : WebApplicationFactory<Program>
{
    private const string SqlConn = "Server=localhost,1433;User Id=sa;Password=RideSync_Pass123!;TrustServerCertificate=True";
    private readonly string _dbName = $"RideSyncTest_{Guid.NewGuid():N}";
    private readonly Dictionary<string, string> _settings;

    public WebFactory(Dictionary<string, string>? overrides = null)
    {
        _settings = new()
        {
            ["ConnectionStrings:Default"] = $"{SqlConn};Database={_dbName}",
            ["ConnectionStrings:Redis"] = "localhost:6379,defaultDatabase=15,abortConnect=false",
        };
        if (overrides is not null) foreach (var kv in overrides) _settings[kv.Key] = kv.Value;

        using var admin = ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true");
        admin.GetServer("localhost", 6379).FlushDatabase(15);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        foreach (var kv in _settings) builder.UseSetting(kv.Key, kv.Value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            SqlConnection.ClearAllPools();
            using var c = new SqlConnection(SqlConn); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}];";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    /// A valid ride request: ~4 km across Dhaka.
    public static RideSync.Web.Services.RideRequestInput Input(string pickup, string drop, string vehicle = "car", string payment = "cash") =>
        new(pickup, drop, 23.7461, 90.3742, 23.7937, 90.4066, vehicle, payment);

    /// Runs code in its own DI scope — the equivalent of one independent HTTP request.
    public async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var s = Services.CreateScope();
        return await work(s.ServiceProvider);
    }

    public Task<Guid> NewUser(string role, long balanceCents = 0, string? vehicleType = null) => InScope(async sp =>
    {
        var db = sp.GetRequiredService<AppDb>();
        var u = new User { Name = $"T-{role}", Email = $"{Guid.NewGuid():N}@t.dev", Role = role, PasswordHash = "x", BalanceCents = balanceCents, VehicleType = vehicleType };
        db.Users.Add(u); await db.SaveChangesAsync(); return u.Id;
    });

    public Task<long> Balance(Guid id) => InScope(sp => Task.FromResult(
        sp.GetRequiredService<AppDb>().Users.Where(u => u.Id == id).Select(u => u.BalanceCents).First()));
}
