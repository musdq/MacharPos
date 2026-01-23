using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;

    public CustomersController(AppDbContext db, IStationContext stationCtx)
    {
        _db = db;
        _stationCtx = stationCtx;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool showInactive = false)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        var q = _db.Customers.AsNoTracking().Where(c => c.StationId == stationId);
        if (!showInactive) q = q.Where(c => c.IsActive);

        var customers = await q.OrderBy(c => c.Name).ToListAsync();

        ViewBag.StationName = station.Name;
        ViewBag.ShowInactive = showInactive;

        return View(customers);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public IActionResult Create() => View(new CustomerEditVm { IsActive = true });

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerEditVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        vm.Name = (vm.Name ?? "").Trim();
        vm.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
        vm.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();

        if (!ModelState.IsValid) return View(vm);

        var exists = await _db.Customers.AnyAsync(c => c.StationId == stationId && c.Name.ToLower() == vm.Name.ToLower());
        if (exists)
        {
            ModelState.AddModelError("", "Customer name already exists for this station.");
            return View(vm);
        }

        var customer = new Customer
        {
            StationId = stationId,
            Name = vm.Name,
            Phone = vm.Phone,
            Address = vm.Address,
            IsActive = vm.IsActive
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Customer created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.StationId == stationId);
        if (c is null) return NotFound();

        var vm = new CustomerEditVm
        {
            Id = c.Id,
            Name = c.Name,
            Phone = c.Phone,
            Address = c.Address,
            IsActive = c.IsActive
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CustomerEditVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id && x.StationId == stationId);
        if (c is null) return NotFound();

        // Protect Walk-in record
        if (c.Name == "Walk-in" && vm.Name.Trim() != "Walk-in")
        {
            ModelState.AddModelError("", "Walk-in customer name cannot be changed.");
            return View(vm);
        }

        vm.Name = (vm.Name ?? "").Trim();
        vm.Phone = string.IsNullOrWhiteSpace(vm.Phone) ? null : vm.Phone.Trim();
        vm.Address = string.IsNullOrWhiteSpace(vm.Address) ? null : vm.Address.Trim();

        if (!ModelState.IsValid) return View(vm);

        c.Name = vm.Name;
        c.Phone = vm.Phone;
        c.Address = vm.Address;
        c.IsActive = vm.IsActive;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Customer updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id && x.StationId == stationId);
        if (c is null) return NotFound();

        if (c.Name == "Walk-in")
        {
            TempData["Err"] = "Walk-in customer cannot be deactivated.";
            return RedirectToAction(nameof(Index));
        }

        c.IsActive = !c.IsActive;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // One-time helper: ensure Walk-in exists for this station
    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnsureWalkIn()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var exists = await _db.Customers.AnyAsync(c => c.StationId == stationId && c.Name == "Walk-in");
        if (!exists)
        {
            _db.Customers.Add(new Customer
            {
                StationId = stationId,
                Name = "Walk-in",
                IsActive = true
            });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Walk-in customer created for this station.";
        }
        else
        {
            TempData["Ok"] = "Walk-in already exists for this station.";
        }

        return RedirectToAction(nameof(Index));
    }
}