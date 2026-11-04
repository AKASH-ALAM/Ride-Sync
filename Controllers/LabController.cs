using Microsoft.AspNetCore.Mvc;
using RideSync.Web.Middleware;
using RideSync.Web.Models.ViewModels;
using RideSync.Web.Services;

namespace RideSync.Web.Controllers;

/// Race Lab — the live viva demo: same parallel load against the safe code and the deliberately buggy twin.
[RateLimit("lab")]
public class LabController(LabService lab) : Controller
{
    public IActionResult Index() => View(new LabVm());

    [HttpPost]
    public async Task<IActionResult> Accept(int drivers = 30) => View("Index", new LabVm
    {
        UnsafeAccept = await lab.RunAcceptAsync(Math.Clamp(drivers, 2, 100), safe: false),
        SafeAccept = await lab.RunAcceptAsync(Math.Clamp(drivers, 2, 100), safe: true)
    });

    [HttpPost]
    public async Task<IActionResult> Wallet(int attempts = 10) => View("Index", new LabVm
    {
        UnsafeWallet = await lab.RunWalletAsync(Math.Clamp(attempts, 2, 50), safe: false),
        SafeWallet = await lab.RunWalletAsync(Math.Clamp(attempts, 2, 50), safe: true)
    });
}
