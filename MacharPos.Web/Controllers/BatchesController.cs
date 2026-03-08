using DocumentFormat.OpenXml.InkML;
using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.Batches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class BatchesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;

    public BatchesController(AppDbContext db, IStationContext stationCtx)
    {
        _db = db;
        _stationCtx = stationCtx;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        var active = await _db.LpgBatches.AsNoTracking()
            .Where(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active)
            .OrderByDescending(b => b.DateReceived)
            .FirstOrDefaultAsync();

        var batches = await _db.LpgBatches.AsNoTracking()
            .Where(b => b.StationId == stationId)
            .OrderByDescending(b => b.DateReceived)
            .ToListAsync();

        ViewBag.StationName = station.Name;
        ViewBag.ActiveBatch = active;

        return View(batches);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public IActionResult Create()
    {
        return View(new LpgBatch
        {
            DateReceived = DateTime.UtcNow,
            QuantityKg = 10000m,
            Status = LpgBatchStatus.Draft
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LpgBatch model)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        model.BatchNo = (model.BatchNo ?? "").Trim();
        model.SupplierName = string.IsNullOrWhiteSpace(model.SupplierName) ? null : model.SupplierName.Trim();

        if (string.IsNullOrWhiteSpace(model.BatchNo))
            ModelState.AddModelError("", "Batch number is required.");

        if (model.QuantityKg <= 0)
            ModelState.AddModelError("", "QuantityKg must be greater than zero.");

        var exists = await _db.LpgBatches.AnyAsync(b => b.StationId == stationId && b.BatchNo.ToLower() == model.BatchNo.ToLower());
        if (exists)
            ModelState.AddModelError("", "Batch number already exists for this station.");

        if (!ModelState.IsValid)
            return View(model);

        model.StationId = stationId;
        model.SoldKg = 0m;
        model.Status = LpgBatchStatus.Draft;

        _db.LpgBatches.Add(model);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Batch created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var batch = await _db.LpgBatches.FirstOrDefaultAsync(b => b.Id == id && b.StationId == stationId);
        if (batch is null) return NotFound();

        if (batch.Status == LpgBatchStatus.Closed)
        {
            TempData["Err"] = "Closed batch cannot be activated.";
            return RedirectToAction(nameof(Index));
        }

        var otherActive = await _db.LpgBatches
            .Where(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active && b.Id != id)
            .ToListAsync();

        foreach (var b in otherActive)
            b.Status = LpgBatchStatus.Closed;

        batch.Status = LpgBatchStatus.Active;
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Batch {batch.BatchNo} is now Active.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var batch = await _db.LpgBatches.FirstOrDefaultAsync(b => b.Id == id && b.StationId == stationId);
        if (batch is null) return NotFound();

        batch.Status = LpgBatchStatus.Closed;
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Batch {batch.BatchNo} closed.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(Guid id, int page = 1, int pageSize = 20)
    {
        var batch = await _db.LpgBatches
            .FirstOrDefaultAsync(x => x.Id == id);

        if (batch == null)
            return NotFound();

        var query = _db.SaleItems
            .Include(x => x.Sale)
            .Where(x => x.LpgBatchId == id);

        var totalSales = await query.SumAsync(x => x.LineTotal);
        var totalCost = await query.SumAsync(x => x.TotalCostAtSale ?? 0);
        var totalKgSold = await query.SumAsync(x => x.LpgKg ?? 0);
        var totalCylinders = await query.CountAsync();

        var totalItems = await query.CountAsync();

        var sales = await query
            .OrderByDescending(x => x.Sale.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new BatchDetailsVM
        {
            Batch = batch,
            Sales = sales,

            TotalKgSold = totalKgSold,
            TotalSalesAmount = totalSales,
            TotalCost = totalCost,
            GrossProfit = totalSales - totalCost,
            TotalCylindersSold = totalCylinders,

            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(vm);
    }
}