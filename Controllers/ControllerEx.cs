using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RideSync.Web.Services;

namespace RideSync.Web.Controllers;

public static class ControllerEx
{
    public static Guid UserId(this Controller c) => Guid.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// fetch() calls from the map UI send this header and get JSON; plain form posts get a redirect + flash message.
    public static bool IsAjax(this Controller c) => c.Request.Headers["X-Requested-With"] == "fetch";

    public static IActionResult Flash<T>(this Controller c, Result<T> r, string successMessage, string redirectAction, string controller = "Rides")
    {
        var message = r.Ok ? successMessage : r.Error;
        if (c.IsAjax()) return c.Json(new { ok = r.Ok, message });
        c.TempData[r.Ok ? "ok" : "err"] = message;
        return c.RedirectToAction(redirectAction, controller);
    }
}
