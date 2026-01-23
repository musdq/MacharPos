using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.DailyClose;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class DailyCloseController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;
    private readonly UserManager<ApplicationUser> _users;

    public DailyCloseController(AppDbContext db, IStationContext stationCtx, UserManager<ApplicationUser> users)
    {
        _db = db;
        _stationCtx = stationCtx;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date = null)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        var closeDate = (date?.Date ?? DateTime.UtcNow.Date); // date-only
        // For querying timestamptz (CreatedAt), ensure UTC kinds
        var utcStart = DateTime.SpecifyKind(closeDate, DateTimeKind.Utc);
        var utcEnd = utcStart.AddDays(1);

        var existing = await _db.DailyCloses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StationId == stationId && x.CloseDate == closeDate);

        // Live recompute for preview (even if closed)
        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcStart && s.CreatedAt < utcEnd)
            .ToListAsync();

        var liveTotal = sales.Sum(s => s.Total);
        var liveTx = sales.Count;

        var liveCash = sales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.Total);
        var livePos = sales.Where(s => s.PaymentMethod == PaymentMethod.POS).Sum(s => s.Total);
        var liveTransfer = sales.Where(s => s.PaymentMethod == PaymentMethod.Transfer).Sum(s => s.Total);

        var liveLpgKg = sales.SelectMany(s => s.Items).Sum(i => i.LpgKg ?? 0m);

        var activeBatch = await _db.LpgBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        var vm = new DailyCloseVm
        {
            StationName = station.Name,
            CloseDate = closeDate,
            Existing = existing,
            LiveTotalSales = liveTotal,
            LiveTotalTransactions = liveTx,
            LiveCashTotal = liveCash,
            LivePosTotal = livePos,
            LiveTransferTotal = liveTransfer,
            LiveTotalLpgKg = liveLpgKg,
            LiveActiveBatchRemainingKg = activeBatch?.RemainingKg ?? 0m,
            CashCounted = existing?.CashCounted,
            Notes = existing?.Notes
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "StationManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseDay(DateTime closeDate, decimal? cashCounted, string? notes)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        closeDate = closeDate.Date;

        var existing = await _db.DailyCloses.FirstOrDefaultAsync(x => x.StationId == stationId && x.CloseDate == closeDate);
        if (existing is not null && existing.IsClosed)
        {
            TempData["Err"] = "This day is already closed. Only SuperAdmin can modify after close.";
            return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
        }

        var utcStart = DateTime.SpecifyKind(closeDate, DateTimeKind.Utc);
        var utcEnd = utcStart.AddDays(1);

        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcStart && s.CreatedAt < utcEnd)
            .ToListAsync();

        var total = sales.Sum(s => s.Total);
        var tx = sales.Count;

        var cash = sales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.Total);
        var pos = sales.Where(s => s.PaymentMethod == PaymentMethod.POS).Sum(s => s.Total);
        var transfer = sales.Where(s => s.PaymentMethod == PaymentMethod.Transfer).Sum(s => s.Total);

        var lpgKg = sales.SelectMany(s => s.Items).Sum(i => i.LpgKg ?? 0m);

        var activeBatch = await _db.LpgBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        var remainingKg = activeBatch?.RemainingKg ?? 0m;

        var variance = cashCounted.HasValue ? (cashCounted.Value - cash) : (decimal?)null;

        if (existing is null)
        {
            existing = new DailyClose
            {
                StationId = stationId,
                CloseDate = closeDate
            };
            _db.DailyCloses.Add(existing);
        }

        existing.TotalSales = total;
        existing.TotalTransactions = tx;
        existing.CashTotal = cash;
        existing.PosTotal = pos;
        existing.TransferTotal = transfer;
        existing.TotalLpgKg = lpgKg;
        existing.ActiveBatchRemainingKg = remainingKg;

        existing.CashCounted = cashCounted;
        existing.CashVariance = variance;
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        existing.IsClosed = true;
        existing.ClosedByUserId = user.Id;
        existing.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Day closed successfully.";
        return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
    }

    /// <summary>
    /// SuperAdmin-only: Recalculate and overwrite the closed snapshot (for late credit sales, etc.)
    /// Requires a reason for audit.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuperRecalculate(DateTime closeDate, string reason, decimal? cashCounted = null, string? notes = null)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        closeDate = closeDate.Date;

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Err"] = "Reason is required.";
            return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
        }

        var dc = await _db.DailyCloses.FirstOrDefaultAsync(x => x.StationId == stationId && x.CloseDate == closeDate);
        if (dc is null)
        {
            TempData["Err"] = "No daily close record exists for that date yet.";
            return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
        }

        var utcStart = DateTime.SpecifyKind(closeDate, DateTimeKind.Utc);
        var utcEnd = utcStart.AddDays(1);

        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcStart && s.CreatedAt < utcEnd)
            .ToListAsync();

        var total = sales.Sum(s => s.Total);
        var tx = sales.Count;
        var cash = sales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.Total);
        var pos = sales.Where(s => s.PaymentMethod == PaymentMethod.POS).Sum(s => s.Total);
        var transfer = sales.Where(s => s.PaymentMethod == PaymentMethod.Transfer).Sum(s => s.Total);
        var lpgKg = sales.SelectMany(s => s.Items).Sum(i => i.LpgKg ?? 0m);

        var activeBatch = await _db.LpgBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        var remainingKg = activeBatch?.RemainingKg ?? 0m;

        dc.TotalSales = total;
        dc.TotalTransactions = tx;
        dc.CashTotal = cash;
        dc.PosTotal = pos;
        dc.TransferTotal = transfer;
        dc.TotalLpgKg = lpgKg;
        dc.ActiveBatchRemainingKg = remainingKg;

        // allow super-admin to adjust cash-counted too if provided
        if (cashCounted.HasValue) dc.CashCounted = cashCounted.Value;
        if (!string.IsNullOrWhiteSpace(notes)) dc.Notes = notes.Trim();

        dc.CashVariance = dc.CashCounted.HasValue ? (dc.CashCounted.Value - dc.CashTotal) : (decimal?)null;

        dc.ModifiedBySuperAdminId = user.Id;
        dc.ModifiedAtUtc = DateTime.UtcNow;
        dc.ModificationReason = reason.Trim();

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Closed day recalculated and updated (SuperAdmin).";
        return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
    }
}