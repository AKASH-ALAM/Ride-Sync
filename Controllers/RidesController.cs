using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Middleware;
using RideSync.Web.Models;
using RideSync.Web.Models.ViewModels;
using RideSync.Web.Services;

namespace RideSync.Web.Controllers;

[Authorize]
public class RidesController(RideService rides, AppDb db) : Controller
{
    /// One URL, two screens: map + panel, the panel depends on the logged-in role. The panel is re-fetched by JS on every realtime event.
    public IActionResult Index() => View(User.IsInRole(Roles.Driver) ? "Driver" : "Rider");

    public async Task<IActionResult> Panel()
    {
        var uid = this.UserId();
        if (User.IsInRole(Roles.Driver))
        {
            var active = await rides.ActiveAsync(uid);
            var me = await db.Users.AsNoTracking().FirstAsync(u => u.Id == uid);
            var today = DateTime.UtcNow.Date;
            var done = await db.Rides.AsNoTracking().Where(r => r.DriverId == uid && r.Status == RideStatus.Completed && r.CompletedAt >= today).Select(r => r.Fare).ToListAsync();
            return PartialView("_DriverPanel", new DriverPanelVm
            {
                Active = active, Available = active is null ? await rides.AvailableAsync(me.VehicleType) : [],
                TodayEarnings = done.Sum(), TodayTrips = done.Count, Rating = me.AvgRating, RatingCount = me.RatingCount
            });
        }
        var balance = await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.BalanceCents).FirstAsync() / 100.0;
        var activeRide = await rides.ActiveAsync(uid);
        return PartialView("_RiderPanel", new RiderPanelVm { Active = activeRide, Unrated = activeRide is null ? await rides.UnratedAsync(uid) : null, Balance = balance });
    }

    [HttpGet, Authorize(Roles = Roles.Rider)]
    public async Task<IActionResult> Estimate(double plat, double plng, double dlat, double dlng)
    {
        var r = await rides.EstimateAsync(plat, plng, dlat, dlng);
        return r.Ok ? Json(r.Value) : BadRequest(new { message = r.Error });
    }

    [HttpPost, Authorize(Roles = Roles.Rider), RateLimit("ride-request")]
    public async Task<IActionResult> RequestRide(string pickup, string drop, double plat, double plng, double dlat, double dlng,
        string vehicle, string payment, string idempotencyKey) =>
        this.Flash(await rides.RequestAsync(this.UserId(), new RideRequestInput(pickup, drop, plat, plng, dlat, dlng, vehicle, payment), idempotencyKey),
            "Looking for a driver near you…", "Index");

    [HttpPost, Authorize(Roles = Roles.Driver)]
    public async Task<IActionResult> Accept(Guid id) =>
        this.Flash(await rides.AcceptAsync(id, this.UserId()), "Ride accepted — head to the pickup point.", "Index");

    [HttpPost]
    public async Task<IActionResult> Status(Guid id, string status)
    {
        var r = await rides.UpdateStatusAsync(id, this.UserId(), status);
        var msg = status == RideStatus.Completed && r.Ok && User.IsInRole(Roles.Driver)
            ? $"Trip completed — ৳{r.Value!.Fare:0} added to your wallet 💰"
            : $"Ride is now: {status.Replace('_', ' ')}";
        return this.Flash(r, msg, "Index");
    }

    [HttpPost, Authorize(Roles = Roles.Rider)]
    public async Task<IActionResult> Rate(Guid id, int stars) =>
        this.Flash(await rides.RateAsync(id, this.UserId(), stars), "Thanks for rating your driver!", "Index");

    public async Task<IActionResult> History() => View(new HistoryVm { Rides = await rides.HistoryAsync(this.UserId(), 100) });

    public async Task<IActionResult> Receipt(Guid id)
    {
        var ride = await rides.GetForUserAsync(id, this.UserId());
        return ride is null ? NotFound() : View(ride);
    }

    public static string RideJson(Ride? r) => r is null ? "null" : JsonSerializer.Serialize(new
    {
        id = r.Id, status = r.Status, plat = r.PickupLat, plng = r.PickupLng, dlat = r.DropLat, dlng = r.DropLng
    });
}
