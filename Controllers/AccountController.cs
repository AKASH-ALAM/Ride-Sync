using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using RideSync.Web.Middleware;
using RideSync.Web.Models;
using RideSync.Web.Models.ViewModels;
using RideSync.Web.Services;

namespace RideSync.Web.Controllers;

[RateLimit("auth")] // strict per-IP limit on login/register: brute-force protection
public class AccountController(AccountService accounts) : Controller
{
    [HttpGet] public IActionResult Register(string? role) => View(new RegisterVm { Role = role == Roles.Driver ? Roles.Driver : Roles.Rider });

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        if (vm.Role is not (Roles.Rider or Roles.Driver)) { ModelState.AddModelError("Role", "Invalid role"); return View(vm); }

        var r = await accounts.RegisterAsync(vm.Name, vm.Email, vm.Password, vm.Role, vm.VehicleNumber, vm.VehicleType);
        if (!r.Ok) { ModelState.AddModelError("", r.Error!); return View(vm); }
        await SignIn(r.Value!);
        return RedirectToAction("Index", "Rides");
    }

    [HttpGet] public IActionResult Login() => View(new LoginVm());

    [HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await accounts.LoginAsync(vm.Email, vm.Password);
        if (user is null) { ModelState.AddModelError("", "Invalid email or password"); return View(vm); }
        await SignIn(user);
        return RedirectToAction("Index", "Rides");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index", "Home");
    }

    private Task SignIn(User u) => HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Name), new Claim(ClaimTypes.Role, u.Role)], "Cookies")));
}
