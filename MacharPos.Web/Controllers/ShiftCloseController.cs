using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.ShiftClose;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class ShiftCloseController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;
    private readonly UserManager<Auth.ApplicationUser> _users;

    public ShiftCloseController(AppDbContext db, IStationContext stationCtx, UserManager<Auth.ApplicationUser> users)
    {
        _db = db;
        _stationCtx = stationCtx;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        var items = await _db.ShiftCloses.AsNoTracking()
            .Where(x => x.StationId == stationId)
            .OrderByDescending(x => x.ShiftStartUtc)
            .Take(50)
            .ToListAsync();

        ViewBag.StationName = station.Name;

        return View(items);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> New()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var hasOpen = await _db.ShiftCloses.AnyAsync(x => x.StationId == stationId && !x.IsClosed);
        if (hasOpen)
        {
            TempData["Err"] = "You already have an open shift. Close it before starting a new one.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ShiftCloseCreateVm { ShiftDate = BusinessTime.TodayLocalDateOnly(), ShiftName = "Morning" });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ShiftCloseCreateVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        vm.ShiftName = (vm.ShiftName ?? "").Trim();
        if (!ModelState.IsValid) return View(vm);

        var hasOpen = await _db.ShiftCloses.AnyAsync(x => x.StationId == stationId && !x.IsClosed);
        if (hasOpen)
        {
            TempData["Err"] = "You already have an open shift. Close it before starting a new one.";
            return RedirectToAction(nameof(Index));
        }

        var shiftDate = (vm.ShiftDate ?? BusinessTime.TodayLocalDateOnly()).Date;

        var entity = new ShiftClose
        {
            StationId = stationId,
            ShiftDate = shiftDate,
            ShiftName = vm.ShiftName,
            ShiftStartUtc = DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            HandedOverTo = string.IsNullOrWhiteSpace(vm.HandedOverTo) ? null : vm.HandedOverTo.Trim(),
            IsClosed = false
        };

        _db.ShiftCloses.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Shift started.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> Close(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var shift = await _db.ShiftCloses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.StationId == stationId);

        if (shift is null) return NotFound();

        var now = DateTime.UtcNow;
        var stats = await ComputeShiftStatsAsync(stationId, shift.ShiftStartUtc, now);

        var vm = new ShiftCloseCloseVm
        {
            Id = shift.Id,
            ShiftName = shift.ShiftName,
            ShiftDate = shift.ShiftDate,
            ShiftStartUtc = shift.ShiftStartUtc,
            ShiftEndUtc = now,

            TotalSales = stats.TotalSales,
            TotalTransactions = stats.TotalTransactions,
            CashCollected = stats.CashCollected,
            PosCollected = stats.PosCollected,
            TransferCollected = stats.TransferCollected,
            TotalLpgKg = stats.TotalLpgKg,
            CreditIssuedTotal = stats.CreditIssuedTotal,
            CreditCollectedTotal = stats.CreditCollectedTotal,

            Notes = shift.Notes,
            HandedOverTo = shift.HandedOverTo
        };

        ViewBag.IsAlreadyClosed = shift.IsClosed;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(ShiftCloseCloseVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var shift = await _db.ShiftCloses.FirstOrDefaultAsync(x => x.Id == vm.Id && x.StationId == stationId);
        if (shift is null) return NotFound();

        if (shift.IsClosed)
        {
            TempData["Err"] = "This shift is already closed.";
            return RedirectToAction(nameof(Index));
        }

        // Recompute stats at close time
        var now = DateTime.UtcNow;
        var stats = await ComputeShiftStatsAsync(stationId, shift.ShiftStartUtc, now);

        shift.ShiftEndUtc = now;
        shift.TotalSales = stats.TotalSales;
        shift.TotalTransactions = stats.TotalTransactions;
        shift.CashCollected = stats.CashCollected;
        shift.PosCollected = stats.PosCollected;
        shift.TransferCollected = stats.TransferCollected;
        shift.TotalLpgKg = stats.TotalLpgKg;
        shift.CreditIssuedTotal = stats.CreditIssuedTotal;
        shift.CreditCollectedTotal = stats.CreditCollectedTotal;

        shift.CashCounted = vm.CashCounted;
        shift.CashVariance = vm.CashCounted is null ? null : (vm.CashCounted.Value - stats.CashCollected);

        shift.Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim();
        shift.HandedOverTo = string.IsNullOrWhiteSpace(vm.HandedOverTo) ? null : vm.HandedOverTo.Trim();

        shift.IsClosed = true;
        shift.ClosedByUserId = user.Id;
        shift.ClosedAtUtc = now;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Shift closed.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<(decimal TotalSales, int TotalTransactions, decimal CashCollected, decimal PosCollected, decimal TransferCollected, decimal TotalLpgKg, decimal CreditIssuedTotal, decimal CreditCollectedTotal)>
        ComputeShiftStatsAsync(Guid stationId, DateTime utcStart, DateTime utcEnd)
    {
        // Sales only (exclude voided)
        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StationId == stationId
                        && s.CreatedAt >= utcStart && s.CreatedAt < utcEnd
                        && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalSales = sales.Sum(s => s.Total);
        var totalTx = sales.Count;

        var cashCollected = sales.Where(s => !s.IsCreditSale && s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.Total);
        var posCollected = sales.Where(s => !s.IsCreditSale && s.PaymentMethod == PaymentMethod.POS).Sum(s => s.Total);
        var transferCollected = sales.Where(s => !s.IsCreditSale && s.PaymentMethod == PaymentMethod.Transfer).Sum(s => s.Total);

        var totalLpgKg = sales.SelectMany(s => s.Items).Sum(i => i.LpgKg ?? 0m);

        // Credit issued during shift
        var creditIssued = sales.Where(s => s.IsCreditSale).Sum(s => s.Total);

        // Credit payments received during shift
        var creditCollected = await _db.CreditPayments.AsNoTracking()
            .Include(p => p.Sale)
            .Where(p => p.Sale!.StationId == stationId
                        && p.Sale!.Status == SaleStatus.Completed
                        && p.ReceivedAtUtc >= utcStart && p.ReceivedAtUtc < utcEnd)
            .SumAsync(p => p.Amount);

        return (totalSales, totalTx, cashCollected, posCollected, transferCollected, totalLpgKg, creditIssued, creditCollected);
    }
}
