using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;

namespace RideSync.Web.Services;

public record RideRequestInput(string Pickup, string Drop, double PickupLat, double PickupLng, double DropLat, double DropLng, string Vehicle, string Payment);
public record Estimate(double DistanceKm, int DurationMin, double Surge, int NearbyDrivers, List<FareOption> Options);
public record AvailableRide(Guid Id, string RiderName, double? RiderRating, string Pickup, string Drop, double DistanceKm, string VehicleType, double Fare, DateTime RequestedAt);

public class RideService(AppDb db, IDistributedLock locks, RedisCache cache, Idempotency idem, WalletService wallet,
    DriverPresence presence, IRideNotifier notifier)
{
    public const string AvailableKey = "rides:available";
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5), LockWait = TimeSpan.FromSeconds(3);

    private static bool ValidCoords(double lat, double lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && (lat != 0 || lng != 0);

    // ───────────────────────── fare estimate (pre-booking) ─────────────────────────
    public async Task<Result<Estimate>> EstimateAsync(double plat, double plng, double dlat, double dlng)
    {
        if (!ValidCoords(plat, plng) || !ValidCoords(dlat, dlng)) return Result<Estimate>.Fail(Outcome.Invalid, "Invalid coordinates");
        var km = Pricing.RoadKm(plat, plng, dlat, dlng);
        if (km < 0.2) return Result<Estimate>.Fail(Outcome.Invalid, "Pickup and drop are too close");
        if (km > 150) return Result<Estimate>.Fail(Outcome.Invalid, "Trips over 150 km are not supported");

        var online = await presence.OnlineCountAsync();
        var pending = await db.Rides.CountAsync(r => r.Status == RideStatus.Pending);
        var surge = Pricing.Surge(pending, online);
        var nearby = (await presence.NearbyAsync(plat, plng, 5)).Count;
        var options = Pricing.Vehicles.Values.Select(v => new FareOption(v.Id, v.Name, v.Icon, v.Seats, v.Tagline,
            Pricing.Fare(v, km, surge), Pricing.DurationMin(v, km))).ToList();
        return Result<Estimate>.Success(new Estimate(km, options.First(o => o.Id == "car").EtaMin, surge, nearby, options));
    }

    // ───────────────────────── request a ride ─────────────────────────
    public async Task<Result<Ride>> RequestAsync(Guid riderId, RideRequestInput i, string? idemKey)
    {
        if (string.IsNullOrWhiteSpace(i.Pickup) || string.IsNullOrWhiteSpace(i.Drop))
            return Result<Ride>.Fail(Outcome.Invalid, "Pickup and drop are required");
        if (!Pricing.Vehicles.TryGetValue(i.Vehicle ?? "", out var vehicle)) return Result<Ride>.Fail(Outcome.Invalid, "Choose a ride type");
        if (i.Payment is not ("cash" or "wallet")) return Result<Ride>.Fail(Outcome.Invalid, "Choose a payment method");

        // Fare is ALWAYS computed on the server from coordinates — never trusted from the browser.
        var est = await EstimateAsync(i.PickupLat, i.PickupLng, i.DropLat, i.DropLng);
        if (!est.Ok) return Result<Ride>.Fail(est.Outcome, est.Error!);
        var option = est.Value!.Options.First(o => o.Id == vehicle.Id);

        if (i.Payment == "wallet")
        {
            var bal = await db.Users.Where(u => u.Id == riderId).Select(u => u.BalanceCents).FirstAsync();
            if (bal < (long)option.Fare * 100) return Result<Ride>.Fail(Outcome.Invalid, "Not enough wallet balance — add money or pay cash");
        }

        // 1) Idempotency: double-click / refresh-resubmit of the same booking returns the first ride instead of making a second.
        var key = idemKey is null ? null : $"{riderId}:{idemKey}";
        if (key is not null)
        {
            var (claimed, existing) = await idem.TryClaimAsync(key);
            if (!claimed)
            {
                if (Guid.TryParse(existing, out var rid) && await db.Rides.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid) is { } ride)
                    return Result<Ride>.Success(ride);
                return Result<Ride>.Fail(Outcome.Conflict, "This request is already being processed");
            }
        }

        try
        {
            // 2) Per-rider lock: "no active ride?" → "create ride" is check-then-act; two tabs could both pass the check.
            await using var gate = await locks.AcquireAsync($"rider:{riderId}", LockTtl, LockWait);
            if (gate is null) { await Release(key); return Result<Ride>.Fail(Outcome.Busy, "Server busy, try again"); }

            if (await db.Rides.AnyAsync(r => r.RiderId == riderId && (r.Status == RideStatus.Pending || r.Status == RideStatus.Accepted
                    || r.Status == RideStatus.DriverArrived || r.Status == RideStatus.InProgress)))
            { await Release(key); return Result<Ride>.Fail(Outcome.Conflict, "You already have an active ride"); }

            var ride = new Ride
            {
                RiderId = riderId, Pickup = i.Pickup.Trim(), Drop = i.Drop.Trim(),
                PickupLat = i.PickupLat, PickupLng = i.PickupLng, DropLat = i.DropLat, DropLng = i.DropLng,
                DistanceKm = est.Value.DistanceKm, DurationMin = option.EtaMin, VehicleType = vehicle.Id,
                PaymentMethod = i.Payment, Surge = est.Value.Surge, Fare = option.Fare
            };
            db.Rides.Add(ride);
            await db.SaveChangesAsync();
            await cache.InvalidateAsync(AvailableKey);
            if (key is not null) await idem.CompleteAsync(key, ride.Id.ToString());
            await notifier.NewRide(ride);
            return Result<Ride>.Success(ride);
        }
        catch { await Release(key); throw; }
    }

    private Task Release(string? key) => key is null ? Task.CompletedTask : idem.ReleaseAsync(key);

    // ───────────────────────── accept (THE race condition) ─────────────────────────
    public async Task<Result<Ride>> AcceptAsync(Guid rideId, Guid driverId)
    {
        // Layer 1 — Redis distributed lock: serialises all drivers competing for this ride, across any number of web servers.
        await using var rideLock = await locks.AcquireAsync($"ride:{rideId}", LockTtl, LockWait);
        if (rideLock is null) return Result<Ride>.Fail(Outcome.Busy, "Ride is being processed, try again");

        // Second lock: a driver can't take two rides at once. Always acquired AFTER the ride lock → consistent order → no deadlock.
        await using var driverLock = await locks.AcquireAsync($"driver:{driverId}", LockTtl, LockWait);
        if (driverLock is null) return Result<Ride>.Fail(Outcome.Busy, "Busy, try again");

        var ride = await db.Rides.FirstOrDefaultAsync(r => r.Id == rideId);
        if (ride is null) return Result<Ride>.Fail(Outcome.NotFound, "Ride not found");
        if (ride.Status != RideStatus.Pending) return Result<Ride>.Fail(Outcome.Conflict, "Ride was already taken by another driver");

        var vehicle = await db.Users.Where(u => u.Id == driverId).Select(u => u.VehicleType).FirstOrDefaultAsync();
        if (vehicle is not null && vehicle != ride.VehicleType)
            return Result<Ride>.Fail(Outcome.Invalid, $"This is a {Pricing.Vehicles[ride.VehicleType].Name} ride — your vehicle is a {Pricing.Vehicles[vehicle].Name}");

        if (await db.Rides.AnyAsync(r => r.DriverId == driverId &&
                (r.Status == RideStatus.Accepted || r.Status == RideStatus.DriverArrived || r.Status == RideStatus.InProgress)))
            return Result<Ride>.Fail(Outcome.Conflict, "You already have an active ride");

        ride.DriverId = driverId; ride.Status = RideStatus.Accepted; ride.AcceptedAt = DateTime.UtcNow;

        // Layer 2 — optimistic concurrency (SQL Server rowversion): UPDATE … WHERE Id=@id AND RowVersion=@old. If the lock ever fails
        // (TTL expiry, Redis failover) the loser updates 0 rows and EF throws. Lock = ordering/efficiency, DB check = correctness.
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Result<Ride>.Fail(Outcome.Conflict, "Ride was just taken by another driver"); }

        await cache.InvalidateAsync(AvailableKey);
        await notifier.RideChanged(ride);
        await notifier.RideTaken();
        return Result<Ride>.Success(ride);
    }

    /// DELIBERATELY BUGGY twin of AcceptAsync, used only by the Race Lab to show the bug the real one prevents.
    public async Task<Result<Ride>> UnsafeAcceptAsync(Guid rideId, Guid driverId)
    {
        var ride = await db.Rides.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rideId);   // 1. READ
        if (ride is null) return Result<Ride>.Fail(Outcome.NotFound, "Ride not found");
        if (ride.Status != RideStatus.Pending) return Result<Ride>.Fail(Outcome.Conflict, "Ride was already taken");
        await Task.Delay(50);                                                                 // 2. window where others pass the same check
        // 3. WRITE — blind update: no lock, no "WHERE status='pending'", no version → last writer wins, everyone told "success".
        await db.Rides.Where(r => r.Id == rideId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.DriverId, driverId).SetProperty(r => r.Status, RideStatus.Accepted));
        return Result<Ride>.Success(ride);
    }

    // ───────────────────────── status changes ─────────────────────────
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [RideStatus.Pending] = [RideStatus.Cancelled],
        [RideStatus.Accepted] = [RideStatus.DriverArrived, RideStatus.Cancelled],
        [RideStatus.DriverArrived] = [RideStatus.InProgress, RideStatus.Cancelled],
        [RideStatus.InProgress] = [RideStatus.Completed],
    };

    public async Task<Result<Ride>> UpdateStatusAsync(Guid rideId, Guid userId, string newStatus)
    {
        await using var gate = await locks.AcquireAsync($"ride:{rideId}", LockTtl, LockWait);
        if (gate is null) return Result<Ride>.Fail(Outcome.Busy, "Ride is being processed, try again");

        var ride = await db.Rides.FirstOrDefaultAsync(r => r.Id == rideId);
        if (ride is null) return Result<Ride>.Fail(Outcome.NotFound, "Ride not found");
        if (ride.RiderId != userId && ride.DriverId != userId) return Result<Ride>.Fail(Outcome.Forbidden, "Not your ride");
        if (!Transitions.TryGetValue(ride.Status, out var allowed) || !allowed.Contains(newStatus))
            return Result<Ride>.Fail(Outcome.Invalid, $"Cannot change status from {ride.Status} to {newStatus}");
        if (newStatus != RideStatus.Cancelled && ride.DriverId != userId)
            return Result<Ride>.Fail(Outcome.Forbidden, "Only the driver can advance the ride");

        // Completing a ride and paying the driver is ONE transaction: either both happen or neither (no "completed but unpaid" ride).
        await using var tx = await db.Database.BeginTransactionAsync();
        ride.Status = newStatus;
        if (newStatus == RideStatus.Completed) ride.CompletedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Result<Ride>.Fail(Outcome.Conflict, "Ride changed concurrently, retry"); }

        if (newStatus == RideStatus.Completed && ride.DriverId is { } driver)
        {
            var cents = (long)Math.Round(ride.Fare * 100);
            var cash = ride.PaymentMethod == "cash";
            // Wallet ride: atomic debit of the rider (one UPDATE … WHERE Balance >= fare). If the rider can no longer cover it, settle in cash.
            if (!cash && await wallet.DebitAsync(ride.RiderId, cents, "Ride payment", ride.Id) is null)
            {
                cash = true;
                ride.PaymentMethod = "cash";
                await db.SaveChangesAsync();
            }
            // The driver's earnings are credited to their wallet for EVERY completed ride.
            await wallet.CreditAsync(driver, cents, cash ? "Ride earning (cash trip)" : "Ride earning", ride.Id);
        }
        await tx.CommitAsync();
        await cache.InvalidateAsync(AvailableKey);
        await notifier.RideChanged(ride);
        if (newStatus == RideStatus.Cancelled) await notifier.RideTaken();   // list of open requests changed for drivers
        return Result<Ride>.Success(ride);
    }

    // ───────────────────────── rating (race: rate exactly once) ─────────────────────────
    public async Task<Result<int>> RateAsync(Guid rideId, Guid riderId, int stars)
    {
        if (stars is < 1 or > 5) return Result<int>.Fail(Outcome.Invalid, "Rating must be 1–5");

        // ONE atomic statement decides "has this ride been rated yet?" — a read-then-write check would let a double-click rate twice.
        var rows = await db.Rides.Where(r => r.Id == rideId && r.RiderId == riderId && r.Status == RideStatus.Completed && r.DriverRating == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DriverRating, stars));
        if (rows == 0) return Result<int>.Fail(Outcome.Conflict, "This ride can't be rated (already rated, not completed, or not yours)");

        var driverId = await db.Rides.Where(r => r.Id == rideId).Select(r => r.DriverId).FirstAsync();
        await db.Users.Where(u => u.Id == driverId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.RatingSum, u => u.RatingSum + stars).SetProperty(u => u.RatingCount, u => u.RatingCount + 1));
        return Result<int>.Success(stars);
    }

    // ───────────────────────── reads ─────────────────────────
    /// Hot path: every online driver loads this. Cache-aside in Redis (3s TTL) + invalidation on writes.
    public async Task<List<AvailableRide>> AvailableAsync(string? driverVehicle = null)
    {
        var all = await AvailableCachedAsync();
        return driverVehicle is null ? all : all.Where(r => r.VehicleType == driverVehicle).ToList();   // a Bike driver isn't offered XL trips
    }

    private Task<List<AvailableRide>> AvailableCachedAsync() =>
        cache.GetOrCreateAsync(AvailableKey, TimeSpan.FromSeconds(3), async () =>
            await db.Rides.AsNoTracking().Where(r => r.Status == RideStatus.Pending).OrderBy(r => r.RequestedAt).Take(50)
                .Select(r => new AvailableRide(r.Id, r.Rider!.Name, r.Rider.RatingCount == 0 ? null : (double)r.Rider.RatingSum / r.Rider.RatingCount,
                    r.Pickup, r.Drop, r.DistanceKm, r.VehicleType, r.Fare, r.RequestedAt)).ToListAsync());

    public Task<Ride?> ActiveAsync(Guid userId) =>
        db.Rides.AsNoTracking().Include(r => r.Rider).Include(r => r.Driver)
            .Where(r => (r.RiderId == userId || r.DriverId == userId) &&
                (r.Status == RideStatus.Pending || r.Status == RideStatus.Accepted || r.Status == RideStatus.DriverArrived || r.Status == RideStatus.InProgress))
            .OrderByDescending(r => r.RequestedAt).FirstOrDefaultAsync();

    /// Most recent completed ride the rider hasn't rated yet (drives the "Rate your driver" card).
    public Task<Ride?> UnratedAsync(Guid riderId) =>
        db.Rides.AsNoTracking().Include(r => r.Driver)
            .Where(r => r.RiderId == riderId && r.Status == RideStatus.Completed && r.DriverRating == null)
            .OrderByDescending(r => r.CompletedAt).FirstOrDefaultAsync();

    public Task<List<Ride>> HistoryAsync(Guid userId, int take = 50) =>
        db.Rides.AsNoTracking().Include(r => r.Rider).Include(r => r.Driver)
            .Where(r => r.RiderId == userId || r.DriverId == userId).OrderByDescending(r => r.RequestedAt).Take(take).ToListAsync();

    public Task<Ride?> GetForUserAsync(Guid rideId, Guid userId) =>
        db.Rides.AsNoTracking().Include(r => r.Rider).Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.Id == rideId && (r.RiderId == userId || r.DriverId == userId));
}
