using Microsoft.AspNetCore.Mvc;

namespace RideSync.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Rides") : View();
    public IActionResult Error() => View();
}
