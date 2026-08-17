using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Services.Finance;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.Expenses;
using MacharPos.Web.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize(Roles = "Admin,StationManager")]
public class CashReconciliationController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationContext;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICashLedgerService _ledger;

    public CashReconciliationController(
        AppDbContext db,
        IStationContext stationContext,
        UserManager<ApplicationUser> users,
        ICashLedgerService ledger)
    {
        _db = db;
        _stationContext = stationContext;
        _users = users;
        _ledger = ledger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? stationId = null, DateTime? date = null)
    {
        var isAdmin = User.IsInRole("Admin");
        var resolvedStationId = await ResolveStationAsync(stationId, isAdmin);
        var localDate = BusinessTime.AsDateOnlyLocal(date ?? BusinessTime.TodayLocalDate());
        var station = await _db.Stations.AsNoTracking().FirstAsync(x => x.Id == resolvedStationId);
        var existing = await _db.CashReconciliations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StationId == resolvedStationId && x.ReconciliationDate == localDate);

        CashDaySnapshot values;
        if (existing?.IsClosed == true)
        {
            values = new CashDaySnapshot(
                existing.OpeningMainCash, existing.OpeningPettyCash,
                existing.CashSales, existing.CashCreditCollections,
                existing.PaidMainCashExpenses, existing.PaidPettyCashExpenses,
                existing.ManualMainCashNet, existing.ManualPettyCashNet,
                existing.ExpectedMainCash, existing.ExpectedPettyCash);
        }
        else
        {
            values = await _ledger.CalculateDayAsync(resolvedStationId, localDate);
        }

        string? closedBy = null;
        if (!string.IsNullOrWhiteSpace(existing?.ClosedByUserId))
            closedBy = await _db.Users.AsNoTracking().Where(x => x.Id == existing.ClosedByUserId)
                .Select(x => x.Email ?? x.UserName ?? x.Id).FirstOrDefaultAsync();

        return View(new CashReconciliationVm
        {
            StationId = resolvedStationId,
            StationName = station.Name,
            ReconciliationDate = localDate,
            Stations = await LoadStationsAsync(),
            IsAdmin = isAdmin,
            IsClosed = existing?.IsClosed == true,
            ClosedAtLocal = existing?.ClosedAtUtc is null ? null : BusinessTime.ToLocal(existing.ClosedAtUtc.Value),
            ClosedBy = closedBy,
            OpeningMainCash = values.OpeningMainCash,
            OpeningPettyCash = values.OpeningPettyCash,
            CashSales = values.CashSales,
            CashCreditCollections = values.CashCreditCollections,
            PaidMainCashExpenses = values.PaidMainCashExpenses,
            PaidPettyCashExpenses = values.PaidPettyCashExpenses,
            ManualMainCashNet = values.ManualMainCashNet,
            ManualPettyCashNet = values.ManualPettyCashNet,
            ExpectedMainCash = values.ExpectedMainCash,
            ExpectedPettyCash = values.ExpectedPettyCash,
            ActualMainCash = existing?.ActualMainCash,
            ActualPettyCash = existing?.ActualPettyCash,
            MainCashVariance = existing?.IsClosed == true ? existing.MainCashVariance : null,
            PettyCashVariance = existing?.IsClosed == true ? existing.PettyCashVariance : null,
            Notes = existing?.Notes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(
        Guid stationId,
        DateTime reconciliationDate,
        decimal actualMainCash,
        decimal actualPettyCash,
        string? notes)
    {
        var isAdmin = User.IsInRole("Admin");
        var resolvedStationId = await ResolveStationAsync(stationId, isAdmin);
        reconciliationDate = BusinessTime.AsDateOnlyLocal(reconciliationDate);

        if (actualMainCash < 0 || actualPettyCash < 0)
        {
            TempData["Err"] = "Actual main cash and petty cash cannot be negative.";
            return ReconciliationRedirect(resolvedStationId, reconciliationDate);
        }
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (notes?.Length > 1000)
        {
            TempData["Err"] = "Notes must be 1,000 characters or fewer.";
            return ReconciliationRedirect(resolvedStationId, reconciliationDate);
        }

        var existing = await _db.CashReconciliations
            .FirstOrDefaultAsync(x => x.StationId == resolvedStationId && x.ReconciliationDate == reconciliationDate);
        if (existing?.IsClosed == true)
        {
            TempData["Err"] = "This cash reconciliation is already closed. An admin must reopen it first.";
            return ReconciliationRedirect(resolvedStationId, reconciliationDate);
        }

        var snapshot = await _ledger.CalculateDayAsync(resolvedStationId, reconciliationDate);
        existing ??= new CashReconciliation
        {
            StationId = resolvedStationId,
            ReconciliationDate = reconciliationDate
        };
        if (_db.Entry(existing).State == EntityState.Detached)
            _db.CashReconciliations.Add(existing);

        existing.OpeningMainCash = snapshot.OpeningMainCash;
        existing.OpeningPettyCash = snapshot.OpeningPettyCash;
        existing.CashSales = snapshot.CashSales;
        existing.CashCreditCollections = snapshot.CashCreditCollections;
        existing.PaidMainCashExpenses = snapshot.PaidMainCashExpenses;
        existing.PaidPettyCashExpenses = snapshot.PaidPettyCashExpenses;
        existing.ManualMainCashNet = snapshot.ManualMainCashNet;
        existing.ManualPettyCashNet = snapshot.ManualPettyCashNet;
        existing.ExpectedMainCash = snapshot.ExpectedMainCash;
        existing.ExpectedPettyCash = snapshot.ExpectedPettyCash;
        existing.ActualMainCash = actualMainCash;
        existing.ActualPettyCash = actualPettyCash;
        existing.MainCashVariance = actualMainCash - snapshot.ExpectedMainCash;
        existing.PettyCashVariance = actualPettyCash - snapshot.ExpectedPettyCash;
        existing.Notes = notes;
        existing.IsClosed = true;
        existing.ClosedByUserId = _users.GetUserId(User);
        existing.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Cash reconciliation closed and snapshot saved.";
        return ReconciliationRedirect(resolvedStationId, reconciliationDate);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reopen(Guid stationId, DateTime reconciliationDate, string reason)
    {
        reconciliationDate = BusinessTime.AsDateOnlyLocal(reconciliationDate);
        reason = reason?.Trim() ?? "";
        if (reason.Length < 3 || reason.Length > 500)
        {
            TempData["Err"] = "A reopen reason between 3 and 500 characters is required.";
            return ReconciliationRedirect(stationId, reconciliationDate);
        }

        var record = await _db.CashReconciliations
            .FirstOrDefaultAsync(x => x.StationId == stationId && x.ReconciliationDate == reconciliationDate && x.IsClosed);
        if (record is null)
        {
            TempData["Err"] = "No closed reconciliation was found.";
            return ReconciliationRedirect(stationId, reconciliationDate);
        }

        record.IsClosed = false;
        record.ReopenedByUserId = _users.GetUserId(User);
        record.ReopenedAtUtc = DateTime.UtcNow;
        record.ReopenReason = reason;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Cash reconciliation reopened. Live source figures will be refreshed when it is closed again.";
        return ReconciliationRedirect(stationId, reconciliationDate);
    }

    private async Task<Guid> ResolveStationAsync(Guid? requested, bool isAdmin)
    {
        if (!isAdmin)
            return await _stationContext.GetStationIdOrThrowAsync();

        if (requested.HasValue)
        {
            if (await _db.Stations.AnyAsync(x => x.Id == requested.Value && x.IsActive))
                return requested.Value;
            throw new InvalidOperationException("The selected station is not active or does not exist.");
        }

        return await _db.Stations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => x.Id).FirstAsync();
    }

    private Task<List<ExpenseStationOption>> LoadStationsAsync()
        => _db.Stations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new ExpenseStationOption(x.Id, x.Name)).ToListAsync();

    private IActionResult ReconciliationRedirect(Guid stationId, DateTime date)
        => RedirectToAction(nameof(Index), new { stationId, date = date.ToString("yyyy-MM-dd") });
}
