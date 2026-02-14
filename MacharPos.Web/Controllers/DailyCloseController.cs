using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.DailyClose;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

    // Normalizes date for DATE column ("date") => Kind must NOT be Utc
    private static DateTime AsDateOnly(DateTime dt)
        => DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);

    private sealed record CloseStats(
        decimal TotalSales,
        int TotalTransactions,
        decimal CashCollected,
        decimal PosCollected,
        decimal TransferCollected,
        decimal TotalLpgKg,
        decimal ActiveBatchRemainingKg,
        decimal CreditIssuedTotal,
        decimal CreditCollectedTotal,
        decimal CreditCollectedCash,
        decimal CreditCollectedPos,
        decimal CreditCollectedTransfer
    );

    private async Task<CloseStats> ComputeStatsAsync(Guid stationId, DateTime closeDateDateOnly)
    {
        // Sales/Payments are UTC timestamptz: filter using UTC boundaries
        var utcStart = DateTime.SpecifyKind(closeDateDateOnly, DateTimeKind.Utc);
        var utcEnd = utcStart.AddDays(1);

        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.StationId == stationId
                        && s.CreatedAt >= utcStart && s.CreatedAt < utcEnd
                        && s.Status == SaleStatus.Completed)
            .ToListAsync();

        // Credit payments collected today
        var creditPayments = await _db.CreditPayments.AsNoTracking()
            .Include(p => p.Sale)
            .Where(p => p.Sale!.StationId == stationId
                        && p.Sale!.Status == SaleStatus.Completed
                        && p.ReceivedAtUtc >= utcStart && p.ReceivedAtUtc < utcEnd)
            .ToListAsync();

        // Split sales: non-credit vs credit
        var nonCreditSales = sales.Where(s => !s.IsCreditSale && s.PaymentMethod != PaymentMethod.Credit).ToList();
        var creditSales = sales.Where(s => s.IsCreditSale || s.PaymentMethod == PaymentMethod.Credit).ToList();

        // TotalSales = ALL sales value (including credit issued)
        var totalSales = sales.Sum(s => s.Total);
        var totalTx = sales.Count;

        // Money collected from non-credit sales by method
        var cashFromNonCredit = nonCreditSales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.Total);
        var posFromNonCredit = nonCreditSales.Where(s => s.PaymentMethod == PaymentMethod.POS).Sum(s => s.Total);
        var transferFromNonCredit = nonCreditSales.Where(s => s.PaymentMethod == PaymentMethod.Transfer).Sum(s => s.Total);

        // Money collected from credit repayments today by method
        var creditCash = creditPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var creditPos = creditPayments.Where(p => p.Method == PaymentMethod.POS).Sum(p => p.Amount);
        var creditTransfer = creditPayments.Where(p => p.Method == PaymentMethod.Transfer).Sum(p => p.Amount);

        // Cash/POS/Transfer totals shown on Daily Close = money actually received today
        var cashCollected = cashFromNonCredit + creditCash;
        var posCollected = posFromNonCredit + creditPos;
        var transferCollected = transferFromNonCredit + creditTransfer;

        // Credit stats
        var creditIssued = creditSales.Sum(s => s.Total);
        var creditCollected = creditPayments.Sum(p => p.Amount);

        // LPG kg sold today (from sale items)
        var totalLpgKg = sales.SelectMany(s => s.Items).Sum(i => i.LpgKg ?? 0m);

        // Active batch remaining (current)
        var activeBatch = await _db.LpgBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        var remainingKg = activeBatch?.RemainingKg ?? 0m;

        return new CloseStats(
            TotalSales: totalSales,
            TotalTransactions: totalTx,
            CashCollected: cashCollected,
            PosCollected: posCollected,
            TransferCollected: transferCollected,
            TotalLpgKg: totalLpgKg,
            ActiveBatchRemainingKg: remainingKg,
            CreditIssuedTotal: creditIssued,
            CreditCollectedTotal: creditCollected,
            CreditCollectedCash: creditCash,
            CreditCollectedPos: creditPos,
            CreditCollectedTransfer: creditTransfer
        );
    }

    /// <summary>
    /// Returns the most recent tank closing kg prior to closeDate (same station).
    /// If none exists, returns 0.
    /// </summary>
    private async Task<decimal> GetTankOpeningKgAsync(Guid stationId, DateTime closeDate)
    {
        var prev = await _db.DailyCloses.AsNoTracking()
            .Where(x => x.StationId == stationId
                        && x.CloseDate < closeDate
                        && x.TankClosingKg != null)
            .OrderByDescending(x => x.CloseDate)
            .Select(x => x.TankClosingKg!.Value)
            .FirstOrDefaultAsync();

        return prev; // default 0 if none
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date = null)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        // Use DateTime.Today or input date; normalize to Unspecified Kind for DATE column
        var closeDate = AsDateOnly(date ?? DateTime.Today);

        var existing = await _db.DailyCloses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StationId == stationId && x.CloseDate == closeDate);

        var stats = await ComputeStatsAsync(stationId, closeDate);

        // Tank opening = yesterday closing (if any), unless this day already has stored opening
        var inferredOpening = await GetTankOpeningKgAsync(stationId, closeDate);
        var openingKg = existing?.TankOpeningKg ?? inferredOpening;
        var receiptsKg = existing?.TankReceiptsKg ?? 0m;
        var closingKg = existing?.TankClosingKg; // may be null if not set yet

        // Live preview values
        decimal? liveScaleSold = closingKg.HasValue ? (openingKg + receiptsKg) - closingKg.Value : (decimal?)null;
        decimal? liveVariance = liveScaleSold.HasValue ? liveScaleSold.Value - stats.TotalLpgKg : (decimal?)null;

        // OPTIONAL: history for last 14 days (shown inside the same Daily Close page)
        // If your VM doesn't have these fields yet, remove this block + the assignments below.
        var history = await _db.DailyCloses.AsNoTracking()
     .Where(x => x.StationId == stationId && x.CloseDate <= closeDate)
     .OrderByDescending(x => x.CloseDate)
     .Take(14)
     .Select(x => new DailyCloseVm.TankHistoryRowVm(
         x.CloseDate,
         x.TankOpeningKg ?? 0m,
         x.TankReceiptsKg ?? 0m,
         x.TankClosingKg ?? 0m,
         x.TotalLpgKg,                 // non-nullable in entity
         x.TankScaleSoldKg ?? 0m,
         x.TankVarianceKg ?? 0m
     ))
     .ToListAsync();


        var vm = new DailyCloseVm
        {
            StationName = station.Name,
            CloseDate = closeDate,
            Existing = existing,

            LiveTotalSales = stats.TotalSales,
            LiveTotalTransactions = stats.TotalTransactions,
            LiveCashTotal = stats.CashCollected,
            LivePosTotal = stats.PosCollected,
            LiveTransferTotal = stats.TransferCollected,
            LiveTotalLpgKg = stats.TotalLpgKg,
            LiveActiveBatchRemainingKg = stats.ActiveBatchRemainingKg,

            // v2 credit live fields
            LiveCreditIssuedTotal = stats.CreditIssuedTotal,
            LiveCreditCollectedTotal = stats.CreditCollectedTotal,
            LiveCreditCollectedCash = stats.CreditCollectedCash,
            LiveCreditCollectedPos = stats.CreditCollectedPos,
            LiveCreditCollectedTransfer = stats.CreditCollectedTransfer,

            CashCounted = existing?.CashCounted,
            Notes = existing?.Notes,

            // Tank inputs (bind in the form)
            TankClosingKg = existing?.TankClosingKg,
            TankReceiptsKg = existing is null ? null : existing.TankReceiptsKg, // show null if new record (optional)

            // Tank live preview
            LiveTankOpeningKg = openingKg,
            LiveTankScaleSoldKg = liveScaleSold ?? 0m,     // if you prefer, keep nullable in VM instead
            LiveTankVarianceKg = liveVariance ?? 0m,       // if you prefer, keep nullable in VM instead

            // Optional history (requires VM support)
            TankHistory = history
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "StationManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseDay(
        DateTime closeDate,
        decimal? cashCounted,
        string? notes,
        decimal? tankClosingKg,
        decimal? tankReceiptsKg
    )
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        closeDate = AsDateOnly(closeDate);

        var existing = await _db.DailyCloses.FirstOrDefaultAsync(x => x.StationId == stationId && x.CloseDate == closeDate);
        if (existing is not null && existing.IsClosed)
        {
            TempData["Err"] = "This day is already closed. Only SuperAdmin can modify after close.";
            return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
        }

        // Enforce tank closing KG for closure (simple reconciliation requirement)
        if (!tankClosingKg.HasValue)
        {
            TempData["Err"] = "Tank scale closing (kg) is required to close the day.";
            return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
        }

        var stats = await ComputeStatsAsync(stationId, closeDate);

        var variance = cashCounted.HasValue ? (cashCounted.Value - stats.CashCollected) : (decimal?)null;

        if (existing is null)
        {
            existing = new DailyClose
            {
                StationId = stationId,
                CloseDate = closeDate
            };
            _db.DailyCloses.Add(existing);
        }

        existing.TotalSales = stats.TotalSales;
        existing.TotalTransactions = stats.TotalTransactions;

        // money received today
        existing.CashTotal = stats.CashCollected;
        existing.PosTotal = stats.PosCollected;
        existing.TransferTotal = stats.TransferCollected;

        // LPG / batch
        existing.TotalLpgKg = stats.TotalLpgKg;
        existing.ActiveBatchRemainingKg = stats.ActiveBatchRemainingKg;

        // credit v2 snapshot
        existing.CreditIssuedTotal = stats.CreditIssuedTotal;
        existing.CreditCollectedTotal = stats.CreditCollectedTotal;
        existing.CreditCollectedCash = stats.CreditCollectedCash;
        existing.CreditCollectedPos = stats.CreditCollectedPos;
        existing.CreditCollectedTransfer = stats.CreditCollectedTransfer;

        existing.CashCounted = cashCounted;
        existing.CashVariance = variance;
        existing.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // Tank reconciliation snapshot
        var openingKg = existing.TankOpeningKg ?? await GetTankOpeningKgAsync(stationId, closeDate);
        var receiptsKg = tankReceiptsKg ?? existing.TankReceiptsKg; // keep previous if not provided
        if (receiptsKg < 0) receiptsKg = 0m;

        existing.TankOpeningKg = openingKg;
        existing.TankReceiptsKg = receiptsKg;
        existing.TankClosingKg = tankClosingKg.Value;

        existing.TankScaleSoldKg = (openingKg + receiptsKg) - tankClosingKg.Value;
        existing.TankVarianceKg = existing.TankScaleSoldKg - stats.TotalLpgKg;

        existing.IsClosed = true;
        existing.ClosedByUserId = user.Id;
        existing.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Day closed successfully.";
        return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuperRecalculate(
        DateTime closeDate,
        string reason,
        decimal? cashCounted = null,
        string? notes = null,
        decimal? tankClosingKg = null,
        decimal? tankReceiptsKg = null
    )
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        closeDate = AsDateOnly(closeDate);

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

        var stats = await ComputeStatsAsync(stationId, closeDate);

        dc.TotalSales = stats.TotalSales;
        dc.TotalTransactions = stats.TotalTransactions;

        dc.CashTotal = stats.CashCollected;
        dc.PosTotal = stats.PosCollected;
        dc.TransferTotal = stats.TransferCollected;

        dc.TotalLpgKg = stats.TotalLpgKg;
        dc.ActiveBatchRemainingKg = stats.ActiveBatchRemainingKg;

        dc.CreditIssuedTotal = stats.CreditIssuedTotal;
        dc.CreditCollectedTotal = stats.CreditCollectedTotal;
        dc.CreditCollectedCash = stats.CreditCollectedCash;
        dc.CreditCollectedPos = stats.CreditCollectedPos;
        dc.CreditCollectedTransfer = stats.CreditCollectedTransfer;

        if (cashCounted.HasValue) dc.CashCounted = cashCounted.Value;
        if (!string.IsNullOrWhiteSpace(notes)) dc.Notes = notes.Trim();

        dc.CashVariance = dc.CashCounted.HasValue ? (dc.CashCounted.Value - dc.CashTotal) : (decimal?)null;

        // Tank: only change if SuperAdmin provided new inputs; otherwise just re-derive variance vs updated app sold
        if (tankClosingKg.HasValue)
        {
            var openingKg = dc.TankOpeningKg ?? await GetTankOpeningKgAsync(stationId, closeDate);
            var receiptsKg = tankReceiptsKg ?? dc.TankReceiptsKg;
            if (receiptsKg < 0) receiptsKg = 0m;

            dc.TankOpeningKg = openingKg;
            dc.TankReceiptsKg = receiptsKg;
            dc.TankClosingKg = tankClosingKg.Value;

            dc.TankScaleSoldKg = (openingKg + receiptsKg) - tankClosingKg.Value;
            dc.TankVarianceKg = dc.TankScaleSoldKg - stats.TotalLpgKg;
        }
        else if (dc.TankClosingKg.HasValue)
        {
            var openingKg = dc.TankOpeningKg ?? 0m;
            var receiptsKg = dc.TankReceiptsKg;

            dc.TankScaleSoldKg = (openingKg + receiptsKg) - dc.TankClosingKg.Value;
            dc.TankVarianceKg = dc.TankScaleSoldKg - stats.TotalLpgKg;
        }

        dc.ModifiedBySuperAdminId = user.Id;
        dc.ModifiedAtUtc = DateTime.UtcNow;
        dc.ModificationReason = reason.Trim();

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Closed day recalculated and updated (SuperAdmin).";
        return RedirectToAction(nameof(Index), new { date = closeDate.ToString("yyyy-MM-dd") });
    }
}
