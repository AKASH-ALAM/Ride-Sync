using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;

namespace RideSync.Web.Services;

/// Services talk to this interface, not to SignalR — keeps business logic independent of the transport.
public interface IRideNotifier
{
    Task NewRide(Ride ride);          // → online drivers near the pickup
    Task RideTaken();                 // → all drivers: their request list is stale
    Task RideChanged(Ride ride);      // → the rider and the driver of that ride
}

public class RideHub : Hub
{
    // Authenticated via the same cookie as MVC. Clients.User(id) targets every browser tab of that user (id = NameIdentifier claim).
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(Roles.Driver) == true) await Groups.AddToGroupAsync(Context.ConnectionId, "drivers");
        await base.OnConnectedAsync();
    }

    public Task GoOnline(double lat, double lng) => Update(lat, lng, null);

    public async Task GoOffline() => await Presence.GoOfflineAsync(Me);

    /// Heartbeat + position (every ~3 s while online). If the driver is on a trip, relay the position to that trip's rider.
    public async Task UpdateLocation(double lat, double lng, Guid? rideId) => await Update(lat, lng, rideId);

    private async Task Update(double lat, double lng, Guid? rideId)
    {
        if (Context.User?.IsInRole(Roles.Driver) != true || lat is < -90 or > 90 || lng is < -180 or > 180) return;
        await Presence.UpdateAsync(Me, lat, lng);
        if (rideId is { } rid)
        {
            var riderId = await Db.Rides.AsNoTracking().Where(r => r.Id == rid && r.DriverId == Me).Select(r => (Guid?)r.RiderId).FirstOrDefaultAsync();
            if (riderId is { } rider) await Clients.User(rider.ToString()).SendAsync("DriverLocation", new { rideId = rid, lat, lng });
        }
    }

    private Guid Me => Guid.Parse(Context.UserIdentifier!);
    private DriverPresence Presence => Context.GetHttpContext()!.RequestServices.GetRequiredService<DriverPresence>();
    private AppDb Db => Context.GetHttpContext()!.RequestServices.GetRequiredService<AppDb>();
}

public class SignalRRideNotifier(IHubContext<RideHub> hub, DriverPresence presence, ILogger<SignalRRideNotifier> log) : IRideNotifier
{
    public async Task NewRide(Ride ride)
    {
        try
        {
            var near = await presence.NearbyAsync(ride.PickupLat, ride.PickupLng, 15);
            if (near.Count > 0) await hub.Clients.Users(near.Select(x => x.ToString())).SendAsync("Refresh");
        }
        catch (Exception ex) { log.LogWarning(ex, "NewRide notification failed"); }
    }

    public async Task RideTaken()
    {
        try { await hub.Clients.Group("drivers").SendAsync("Refresh"); }
        catch (Exception ex) { log.LogWarning(ex, "RideTaken notification failed"); }
    }

    public async Task RideChanged(Ride ride)
    {
        try
        {
            var users = new List<string> { ride.RiderId.ToString() };
            if (ride.DriverId is { } d) users.Add(d.ToString());
            await hub.Clients.Users(users).SendAsync("Refresh");
        }
        catch (Exception ex) { log.LogWarning(ex, "RideChanged notification failed"); }
    }
}
