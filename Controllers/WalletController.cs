using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RideSync.Web.Data;
using RideSync.Web.Middleware;
using RideSync.Web.Models.ViewModels;
using RideSync.Web.Services;

namespace RideSync.Web.Controllers;

[Authorize, RateLimit("wallet")]
public class WalletController(AppDb db, WalletService wallet) : Controller
{
    private const long MaxCents = 10_000_000;

    public async Task<IActionResult> Index()
    {
        var uid = this.UserId();
        return View(new WalletVm
        {
            Balance = await db.Users.Where(u => u.Id == uid).Select(u => u.BalanceCents).FirstAsync() / 100.0,
            Transactions = await db.Transactions.AsNoTracking().Where(t => t.UserId == uid).OrderByDescending(t => t.CreatedAt).Take(50).ToListAsync()
        });
    }

    [HttpPost]
    public async Task<IActionResult> TopUp(decimal amount)
    {
        var cents = (long)Math.Round(amount * 100);
        if (cents <= 0 || cents > MaxCents) TempData["err"] = "Invalid amount";
        else { await wallet.CreditAsync(this.UserId(), cents, "Wallet top-up"); TempData["ok"] = "Money added"; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(decimal amount)
    {
        var cents = (long)Math.Round(amount * 100);
        if (cents <= 0 || cents > MaxCents) TempData["err"] = "Invalid amount";
        else if (await wallet.DebitAsync(this.UserId(), cents, "Withdrawal") is null) TempData["err"] = "Insufficient balance";
        else TempData["ok"] = "Withdrawal successful";
        return RedirectToAction(nameof(Index));
    }
}
