using MacharPos.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MacharPos.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Action("Index", "Home");
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        returnUrl = Url.Action("Index", "Dashboard");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid login.");
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Invalid login.");
            return View();
        }

        return LocalRedirect(returnUrl!);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Denied() => View();
}
