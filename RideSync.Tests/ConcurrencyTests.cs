using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Models;
using RideSync.Web.Services;
using Xunit.Abstractions;

namespace RideSync.Tests;

public class ConcurrencyTests(ITestOutputHelper output) : IDisposable
{
    private readonly WebFactory _f = new();
    public void Dispose() => _f.Dispose();

    private LabService Lab => _f.Services.GetRequiredService<LabService>();

    /// Releases all calls at the same instant, each in its own scope (own DbContext), like separate HTTP requests.
    private async Task<Result<T>[]> Parallel<T>(int n, Func<IServiceProvider, int, Task<Result<T>>> call)
    {
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, n).Select(async i => { await gate.Task; return await _f.InScope(sp => call(sp, i)); }).ToList();
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Accept_30_drivers_at_once_exactly_one_wins()
    {
        var r = await Lab.RunAcceptAsync(30, safe: true);
        output.WriteLine($"SAFE: winners={r.Winners} rejected={r.Rejected} busy={r.Busy} ({r.Ms} ms)");
        Assert.Equal(1, r.Winners);
        Assert.Equal(29, r.Rejected + r.Busy);
        Assert.NotNull(r.FinalDriver);
    }

    [Fact]
    public async Task UNSAFE_accept_shows_the_race_condition_many_drivers_think_they_won()
    {
        var r = await Lab.RunAcceptAsync(30, safe: false);
        output.WriteLine($"UNSAFE: {r.Winners} drivers were told they got the ride (should be 1)");
        Assert.True(r.Winners > 1, "race did not manifest — the buggy twin should double-assign");
    }

    [Fact]
    public async Task Wallet_parallel_withdrawals_never_overdraw()
    {
        var r = await Lab.RunWalletAsync(10, safe: true);
        Assert.Equal(3, r.Succeeded);
        Assert.Equal(1_000, r.FinalCents);
        Assert.False(r.Overdrawn);
    }

    [Fact]
    public async Task UNSAFE_wallet_double_spends()
    {
        var r = await Lab.RunWalletAsync(10, safe: false);
        output.WriteLine($"UNSAFE wallet: {r.Succeeded} withdrawals succeeded, max possible {r.ExpectedMaxSuccess}, final {r.FinalCents}");
        Assert.True(r.Overdrawn);
    }

    [Fact]
    public async Task One_driver_cannot_accept_two_rides_at_once()
    {
        var (rider1, rider2, driver) = (await _f.NewUser(Roles.Rider), await _f.NewUser(Roles.Rider), await _f.NewUser(Roles.Driver));
        var ride1 = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider1, WebFactory.Input("A", "B"), null))).Value!.Id;
        var ride2 = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider2, WebFactory.Input("C", "D"), null))).Value!.Id;

        var results = await Parallel(2, (sp, i) => sp.GetRequiredService<RideService>().AcceptAsync(i == 0 ? ride1 : ride2, driver));
        Assert.Equal(1, results.Count(r => r.Ok));
    }

    [Fact]
    public async Task Rider_double_tap_creates_only_one_ride()
    {
        var rider = await _f.NewUser(Roles.Rider);
        var results = await Parallel(10, (sp, _) => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null));
        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal(9, results.Count(r => r.Outcome == Outcome.Conflict));
    }

    [Fact]
    public async Task Idempotency_key_returns_same_ride_on_resubmit()
    {
        var rider = await _f.NewUser(Roles.Rider);
        var a = await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), "form-1"));
        var b = await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), "form-1"));
        Assert.True(a.Ok && b.Ok);
        Assert.Equal(a.Value!.Id, b.Value!.Id);
    }

    [Fact]
    public async Task Completed_ride_moves_money_rider_to_driver_and_conserves_total()
    {
        var rider = await _f.NewUser(Roles.Rider, 100_000); var driver = await _f.NewUser(Roles.Driver);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B", payment: "wallet"), null))).Value!;
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, driver))).Ok);
        foreach (var s in new[] { "driver_arrived", "in_progress", "completed" })
            Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, s))).Ok);

        var (r, d) = (await _f.Balance(rider), await _f.Balance(driver));
        Assert.True(d > 0);
        Assert.Equal(100_000, r + d);
    }

    [Fact]
    public async Task Rating_can_only_be_submitted_once_even_with_a_double_click()
    {
        var rider = await _f.NewUser(Roles.Rider); var driver = await _f.NewUser(Roles.Driver);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Value!;
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, driver))).Ok);
        foreach (var s in new[] { "driver_arrived", "in_progress", "completed" })
            Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, s))).Ok);

        var results = await Parallel(10, (sp, _) => sp.GetRequiredService<RideService>().RateAsync(ride.Id, rider, 5));
        Assert.Equal(1, results.Count(r => r.Ok));

        var d = await _f.InScope(sp => Task.FromResult(sp.GetRequiredService<AppDb>().Users.AsNoTracking().First(u => u.Id == driver)));
        Assert.Equal(1, d.RatingCount);      // the aggregate was bumped exactly once too
        Assert.Equal(5, d.RatingSum);
    }

    [Fact]
    public async Task Cannot_rate_a_ride_that_is_not_yours_or_not_completed()
    {
        var rider = await _f.NewUser(Roles.Rider); var other = await _f.NewUser(Roles.Rider);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Value!;
        Assert.False((await _f.InScope(sp => sp.GetRequiredService<RideService>().RateAsync(ride.Id, rider, 5))).Ok);   // not completed
        Assert.False((await _f.InScope(sp => sp.GetRequiredService<RideService>().RateAsync(ride.Id, other, 5))).Ok);   // not theirs
    }

    [Fact]
    public async Task Driver_can_only_take_rides_for_their_own_vehicle_class()
    {
        var rider = await _f.NewUser(Roles.Rider);
        var bikeDriver = await _f.NewUser(Roles.Driver, vehicleType: "bike");
        var carDriver = await _f.NewUser(Roles.Driver, vehicleType: "car");
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B", vehicle: "car"), null))).Value!;

        // not even offered
        Assert.Empty(await _f.InScope(sp => sp.GetRequiredService<RideService>().AvailableAsync("bike")));
        Assert.Single(await _f.InScope(sp => sp.GetRequiredService<RideService>().AvailableAsync("car")));

        // and refused if the bike driver tries anyway (crafted request)
        var refused = await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, bikeDriver));
        Assert.Equal(Outcome.Invalid, refused.Outcome);
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, carDriver))).Ok);
    }

    [Fact]
    public async Task Cash_ride_credits_the_driver_wallet_and_leaves_the_rider_wallet_alone()
    {
        var rider = await _f.NewUser(Roles.Rider, 50_000); var driver = await _f.NewUser(Roles.Driver);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Value!;
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, driver))).Ok);
        foreach (var s in new[] { "driver_arrived", "in_progress" })
            Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, s))).Ok);
        Assert.Equal(0, await _f.Balance(driver));                       // nothing paid out before the trip is finished
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, "completed"))).Ok);

        Assert.Equal(50_000, await _f.Balance(rider));                   // cash: the rider's wallet is untouched
        Assert.Equal((long)ride.Fare * 100, await _f.Balance(driver));   // …but the driver is credited the full fare
    }

    [Fact]
    public async Task Completing_twice_never_pays_the_driver_twice()
    {
        var rider = await _f.NewUser(Roles.Rider); var driver = await _f.NewUser(Roles.Driver);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Value!;
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, driver))).Ok);
        foreach (var s in new[] { "driver_arrived", "in_progress" })
            Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, s))).Ok);

        var results = await Parallel(10, (sp, _) => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, "completed"));   // double-tap
        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal((long)ride.Fare * 100, await _f.Balance(driver));
    }

    [Fact]
    public async Task Wallet_ride_falls_back_to_cash_if_rider_balance_vanished_but_driver_is_still_paid()
    {
        var rider = await _f.NewUser(Roles.Rider, 100_000); var driver = await _f.NewUser(Roles.Driver);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B", payment: "wallet"), null))).Value!;
        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().AcceptAsync(ride.Id, driver))).Ok);
        foreach (var s in new[] { "driver_arrived", "in_progress" })
            Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, s))).Ok);
        await _f.InScope(sp => sp.GetRequiredService<WalletService>().DebitAsync(rider, 100_000, "spent elsewhere"));   // rider drains the wallet mid-trip

        Assert.True((await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, driver, "completed"))).Ok);
        Assert.Equal(0, await _f.Balance(rider));
        Assert.Equal((long)ride.Fare * 100, await _f.Balance(driver));
    }

    [Fact]
    public async Task Wallet_payment_is_refused_up_front_when_balance_is_too_low()
    {
        var rider = await _f.NewUser(Roles.Rider, 100);   // ৳1.00
        var res = await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B", payment: "wallet"), null));
        Assert.Equal(Outcome.Invalid, res.Outcome);
    }

    [Fact]
    public async Task Invalid_status_transition_is_rejected()
    {
        var rider = await _f.NewUser(Roles.Rider);
        var ride = (await _f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Value!;
        var res = await _f.InScope(sp => sp.GetRequiredService<RideService>().UpdateStatusAsync(ride.Id, rider, "completed"));
        Assert.Equal(Outcome.Invalid, res.Outcome);
    }
}
