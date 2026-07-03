using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MacharPos.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult About() => View();

    public IActionResult Directors() => View();

    public IActionResult Contact() => View();

    public IActionResult Portal() => RedirectToAction("Login", "Account");
}
