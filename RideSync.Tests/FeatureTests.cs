using Microsoft.Extensions.DependencyInjection;
using RideSync.Web.Models;
using RideSync.Web.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace RideSync.Tests;

public class PricingTests
{
    [Fact]
    public void Fare_grows_with_distance_and_vehicle_class()
    {
        var bike = Pricing.Fare(Pricing.Vehicles["bike"], 5, 1);
        var car = Pricing.Fare(Pricing.Vehicles["car"], 5, 1);
        var xl = Pricing.Fare(Pricing.Vehicles["xl"], 5, 1);
        Assert.True(bike < car && car < xl);
        Assert.True(Pricing.Fare(Pricing.Vehicles["car"], 10, 1) > car);
    }

    [Fact] public void Minimum_fare_applies() => Assert.Equal(40, Pricing.Fare(Pricing.Vehicles["bike"], 0.2, 1));

    [Theory]
    [InlineData(0, 5, 1.0)]    // plenty of drivers
    [InlineData(4, 2, 1.4)]    // (4+1)/2 = 2.5 → 1 + .25*1.5
    [InlineData(100, 1, 2.0)]  // capped
    [InlineData(3, 0, 1.8)]    // no drivers at all: (3+1)/1 = 4 → 1 + .25*3 = 1.75 → 1.8
    public void Surge_follows_demand_over_supply(int pending, int drivers, double expected) =>
        Assert.Equal(expected, Pricing.Surge(pending, drivers));

    [Fact]
    public void Haversine_is_roughly_right() =>
        Assert.InRange(Pricing.Haversine(23.7461, 90.3742, 23.7937, 90.4066), 6.0, 7.5);   // Dhaka: Mohammadpur → Gulshan
}

public class PresenceTests
{
    [Fact]
    public async Task Only_online_drivers_inside_the_radius_are_found_and_offline_drivers_disappear()
    {
        using var f = new WebFactory();                                    // flushes Redis DB 15
        var presence = f.Services.GetRequiredService<DriverPresence>();
        var near = Guid.NewGuid(); var far = Guid.NewGuid();
        await presence.UpdateAsync(near, 23.7500, 90.3800);               // ~1 km from pickup
        await presence.UpdateAsync(far, 22.3569, 91.7832);                // Chattogram, ~200 km away

        var found = await presence.NearbyAsync(23.7461, 90.3742, 5);
        Assert.Contains(near, found); Assert.DoesNotContain(far, found);
        Assert.Equal(2, await presence.OnlineCountAsync());

        await presence.GoOfflineAsync(near);
        Assert.DoesNotContain(near, await presence.NearbyAsync(23.7461, 90.3742, 5));
        Assert.False(await presence.IsOnlineAsync(near));
    }
}

public class EstimateTests
{
    [Fact]
    public async Task Estimate_returns_three_priced_options_and_rejects_bad_input()
    {
        using var f = new WebFactory();
        var ok = await f.InScope(sp => sp.GetRequiredService<RideService>().EstimateAsync(23.7461, 90.3742, 23.7937, 90.4066));
        Assert.True(ok.Ok); Assert.Equal(3, ok.Value!.Options.Count); Assert.All(ok.Value.Options, o => Assert.True(o.Fare >= 40));

        var same = await f.InScope(sp => sp.GetRequiredService<RideService>().EstimateAsync(23.7461, 90.3742, 23.7461, 90.3742));
        Assert.Equal(Outcome.Invalid, same.Outcome);
        var bad = await f.InScope(sp => sp.GetRequiredService<RideService>().EstimateAsync(999, 90, 23, 90));
        Assert.Equal(Outcome.Invalid, bad.Outcome);
    }

    [Fact]
    public async Task Surge_rises_when_requests_outnumber_online_drivers()
    {
        using var f = new WebFactory();
        for (var i = 0; i < 4; i++)
        {
            var rider = await f.NewUser(Roles.Rider);
            Assert.True((await f.InScope(sp => sp.GetRequiredService<RideService>().RequestAsync(rider, WebFactory.Input("A", "B"), null))).Ok);
        }
        var est = await f.InScope(sp => sp.GetRequiredService<RideService>().EstimateAsync(23.7461, 90.3742, 23.7937, 90.4066));
        Assert.True(est.Value!.Surge > 1.0);
    }
}

public class StaleCookieTests
{
    [Fact]
    public async Task Cookie_for_a_deleted_user_redirects_to_login_instead_of_crashing()
    {
        using var f = new WebFactory();
        var user = await f.NewUser(Roles.Rider);
        var c = f.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });

        // sign in through the real pipeline by crafting the cookie via a test-only login: register through the form
        var page = await c.GetStringAsync("/Account/Register");
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var res = await c.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Name"] = "Gone", ["Email"] = $"{Guid.NewGuid():N}@t.dev", ["Password"] = "secret1", ["Role"] = "Rider", ["__RequestVerificationToken"] = token }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await c.GetAsync("/Rides")).StatusCode);            // logged in

        await f.InScope(async sp =>                                                                       // wipe every user (like a DB reset)
        {
            var db = sp.GetRequiredService<RideSync.Web.Data.AppDb>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Transactions; DELETE FROM Rides; DELETE FROM Users;");
            return 0;
        });

        var after = await c.GetAsync("/Wallet");                                                          // used to throw "Sequence contains no elements"
        Assert.Equal(System.Net.HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Headers.Location!.ToString());
    }
}
