using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;

namespace RideSync.Web.Services;

public record AcceptLabReport(int Drivers, int Winners, int Rejected, int Busy, long Ms, Guid? FinalDriver);
public record WalletLabReport(long StartCents, int Attempts, long AmountEach, int Succeeded, long FinalCents, long ExpectedMaxSuccess)
{
    public bool Overdrawn => FinalCents < 0 || Succeeded > ExpectedMaxSuccess;
}

/// <summary>
/// Race Lab: fires N truly-simultaneous requests at the SAFE code and at the deliberately BUGGY twin, and reports both.
/// Each simulated request gets its own DI scope (= its own DbContext), exactly like separate HTTP requests would.
/// </summary>
public class LabService(IServiceScopeFactory scopes)
{
    public async Task<AcceptLabReport> RunAcceptAsync(int drivers, bool safe)
    {
        Guid rideId; var driverIds = new List<Guid>();
        using (var s = scopes.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDb>();
            var rider = NewUser("Lab Rider", Roles.Rider); db.Users.Add(rider);
            for (var i = 0; i < drivers; i++) { var d = NewUser($"Lab Driver {i + 1}", Roles.Driver); db.Users.Add(d); driverIds.Add(d.Id); }
            var ride = new Ride { RiderId = rider.Id, Pickup = "Lab A", Drop = "Lab B", Fare = 100, PickupLat = 23.78, PickupLng = 90.40, DropLat = 23.81, DropLng = 90.42 };
            db.Rides.Add(ride); await db.SaveChangesAsync(); rideId = ride.Id;
        }

        var gate = new TaskCompletionSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = driverIds.Select(async id =>
        {
            await gate.Task;                                   // release everyone at the same instant
            using var s = scopes.CreateScope();
            var rides = s.ServiceProvider.GetRequiredService<RideService>();
            return safe ? await rides.AcceptAsync(rideId, id) : await rides.UnsafeAcceptAsync(rideId, id);
        }).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        using var s2 = scopes.CreateScope();
        var final = await s2.ServiceProvider.GetRequiredService<AppDb>().Rides.AsNoTracking().FirstAsync(r => r.Id == rideId);
        return new AcceptLabReport(drivers, results.Count(r => r.Ok), results.Count(r => r.Outcome == Outcome.Conflict),
            results.Count(r => r.Outcome == Outcome.Busy), sw.ElapsedMilliseconds, final.DriverId);
    }

    public async Task<WalletLabReport> RunWalletAsync(int attempts, bool safe, long startCents = 10_000, long eachCents = 3_000)
    {
        Guid userId;
        using (var s = scopes.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDb>();
            var u = NewUser("Lab Wallet", Roles.Rider); u.BalanceCents = startCents;
            db.Users.Add(u); await db.SaveChangesAsync(); userId = u.Id;
        }

        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, attempts).Select(async _ =>
        {
            await gate.Task;
            using var s = scopes.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<AppDb>();
            if (safe) return await s.ServiceProvider.GetRequiredService<WalletService>().DebitAsync(userId, eachCents, "Lab withdrawal") is not null;

            // BUGGY read-modify-write: every request reads the same old balance, then writes "old - amount".
            var bal = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.BalanceCents).FirstAsync();
            if (bal < eachCents) return false;
            await Task.Delay(30);
            await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(x => x.SetProperty(p => p.BalanceCents, bal - eachCents));
            return true;
        }).ToList();
        gate.SetResult();
        var ok = (await Task.WhenAll(tasks)).Count(x => x);

        using var s2 = scopes.CreateScope();
        var final = await s2.ServiceProvider.GetRequiredService<AppDb>().Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.BalanceCents).FirstAsync();
        return new WalletLabReport(startCents, attempts, eachCents, ok, final, startCents / eachCents);
    }

    private static User NewUser(string name, string role) =>
        new() { Name = name, Role = role, Email = $"lab-{Guid.NewGuid():N}@lab.local", PasswordHash = "!lab-account-cannot-login" };
}
