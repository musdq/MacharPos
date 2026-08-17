using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var stationMap = await _db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);

        var rows = new List<UserListRowVm>();
        foreach (var u in users.OrderBy(x => x.Email))
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? "";
            var stationName = (u.HomeStationId is Guid sid && stationMap.TryGetValue(sid, out var n)) ? n : "-";

            rows.Add(new UserListRowVm
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? "",
                IsActive = u.LockoutEnd is null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                Role = role,
                HomeStation = stationName
            });
        }

        ViewBag.CreateVm = await BuildCreateVmAsync();
        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserVm vm)
    {
        vm.Stations = await _db.Stations.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new StationOption(s.Id, s.Name))
            .ToListAsync();

        vm.Roles = await _roleManager.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync();

        if (!ModelState.IsValid)
        {
            var users = await LoadUserRowsAsync();
            ViewBag.CreateVm = vm;
            return View("Index", users);
        }

        var exists = await _userManager.FindByEmailAsync(vm.Email);
        if (exists is not null)
        {
            ModelState.AddModelError("", "A user with this email already exists.");
            var users = await LoadUserRowsAsync();
            ViewBag.CreateVm = vm;
            return View("Index", users);
        }

        if (!await _roleManager.RoleExistsAsync(vm.Role))
        {
            ModelState.AddModelError("", "Selected role does not exist.");
            var users = await LoadUserRowsAsync();
            ViewBag.CreateVm = vm;
            return View("Index", users);
        }

        var stationExists = await _db.Stations.AnyAsync(s => s.Id == vm.HomeStationId);
        if (!stationExists)
        {
            ModelState.AddModelError("", "Selected station does not exist.");
            var users = await LoadUserRowsAsync();
            ViewBag.CreateVm = vm;
            return View("Index", users);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            HomeStationId = vm.HomeStationId
        };

        var createResult = await _userManager.CreateAsync(user, vm.Password);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);

            var users = await LoadUserRowsAsync();
            ViewBag.CreateVm = vm;
            return View("Index", users);
        }

        await _userManager.AddToRoleAsync(user, vm.Role);

        TempData["Ok"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var isActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;
        if (isActive)
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        else
            user.LockoutEnd = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreateUserVm> BuildCreateVmAsync()
    {
        var stations = await _db.Stations.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new StationOption(s.Id, s.Name))
            .ToListAsync();

        var roles = await _roleManager.Roles.AsNoTracking()
            .Select(r => r.Name!)
            .OrderBy(x => x)
            .ToListAsync();

        var defaultStation = stations.FirstOrDefault(s => s.Name.Contains("Masaka", StringComparison.OrdinalIgnoreCase))
                             ?? stations.FirstOrDefault();

        return new CreateUserVm
        {
            Stations = stations,
            Roles = roles,
            Role = roles.Contains("Cashier") ? "Cashier" : roles.FirstOrDefault() ?? "Cashier",
            HomeStationId = defaultStation?.Id ?? Guid.Empty
        };
    }

    private async Task<List<UserListRowVm>> LoadUserRowsAsync()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var stationMap = await _db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);

        var rows = new List<UserListRowVm>();
        foreach (var u in users.OrderBy(x => x.Email))
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? "";
            var stationName = (u.HomeStationId is Guid sid && stationMap.TryGetValue(sid, out var n)) ? n : "-";

            rows.Add(new UserListRowVm
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? "",
                IsActive = u.LockoutEnd is null || u.LockoutEnd <= DateTimeOffset.UtcNow,
                Role = role,
                HomeStation = stationName
            });
        }
        return rows;
    }

    [HttpGet]
    public IActionResult FreshStart()
    {
        ViewBag.DatabaseName = _db.Database.GetDbConnection().Database;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FreshStart(string confirmationText)
    {
        if (confirmationText?.Trim() != "START FRESH")
        {
            TempData["Err"] = "Reset cancelled. Type START FRESH exactly.";
            return RedirectToAction(nameof(FreshStart));
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            await _db.CashReconciliations.ExecuteDeleteAsync();
            await _db.CashMovements.ExecuteDeleteAsync();
            await _db.ExpenseRequests.ExecuteDeleteAsync();
            await _db.SaleItems.ExecuteDeleteAsync();
            await _db.CreditPayments.ExecuteDeleteAsync();
            await _db.DailyCloses.ExecuteDeleteAsync();
            await _db.ShiftCloses.ExecuteDeleteAsync();
            await _db.SyncQueue.ExecuteDeleteAsync();
            await _db.Sales.ExecuteDeleteAsync();
            await _db.LpgBatches.ExecuteDeleteAsync();
            await _db.Customers.ExecuteDeleteAsync();

            var stations = await _db.Stations.ToListAsync();
            foreach (var st in stations)
            {
                _db.Customers.Add(new Customer
                {
                    StationId = st.Id,
                    Name = "Walk-in"
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Ok"] = "Fresh start completed successfully.";
            return RedirectToAction(nameof(FreshStart));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["Err"] = $"Fresh start failed: {ex.Message}";
            return RedirectToAction(nameof(FreshStart));
        }
    }
}
