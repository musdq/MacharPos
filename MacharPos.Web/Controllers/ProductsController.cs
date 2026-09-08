using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize(Roles = "Admin,StationManager")]
public class ProductsController : Controller
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? categoryId = null,
        Guid? stationId = null,
        bool showInactive = false)
    {
        await LoadLookupsAsync(categoryId, stationId);
        ViewBag.CategoryId = categoryId;
        ViewBag.StationId = stationId;
        ViewBag.ShowInactive = showInactive;

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Station)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (stationId.HasValue)
            query = query.Where(p => p.StationId == stationId.Value);

        if (!showInactive)
            query = query.Where(p => p.IsActive);

        var products = await query
            .OrderBy(p => p.Station!.Name)
            .ThenBy(p => p.Category!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new Product
        {
            DefaultUnitPrice = 0m,
            UnitType = "Piece",
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model)
    {
        Normalize(model);
        await ValidateReferencesAsync(model);

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(model.CategoryId, model.StationId);
            return View(model);
        }

        _db.Products.Add(model);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Product created.";
        return RedirectToAction(nameof(Index), new { stationId = model.StationId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        await LoadLookupsAsync(product.CategoryId, product.StationId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Product model)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        Normalize(model);
        await ValidateReferencesAsync(model);

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(model.CategoryId, model.StationId);
            return View(model);
        }

        product.Name = model.Name;
        product.Sku = model.Sku;
        product.CategoryId = model.CategoryId;
        product.StationId = model.StationId;
        product.DefaultUnitPrice = model.DefaultUnitPrice;
        product.EquivalentKg = model.EquivalentKg;
        product.IsActive = model.IsActive;
        product.UnitType = "Piece";

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Product updated.";
        return RedirectToAction(nameof(Index), new { stationId = product.StationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpdatePrices(
      decimal pricePerKg,
      Guid? categoryId = null,
      Guid? stationId = null,
      bool showInactive = false)
    {
        if (pricePerKg <= 0)
        {
            TempData["Err"] = "Please enter a valid price per kg.";

            return RedirectToAction(nameof(Index), new
            {
                categoryId,
                stationId,
                showInactive
            });
        }

        if (!stationId.HasValue)
        {
            TempData["Err"] =
                "Please select a station before updating LPG prices.";

            return RedirectToAction(nameof(Index), new
            {
                categoryId,
                stationId,
                showInactive
            });
        }

        var stationExists = await _db.Stations
            .AsNoTracking()
            .AnyAsync(s => s.Id == stationId.Value);

        if (!stationExists)
        {
            TempData["Err"] = "The selected station was not found.";

            return RedirectToAction(nameof(Index), new
            {
                categoryId,
                stationId = (Guid?)null,
                showInactive
            });
        }

        var lpgCategory = await _db.ProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "LPG Refills");

        if (lpgCategory is null)
        {
            TempData["Err"] =
                "The LPG Refills category was not found.";

            return RedirectToAction(nameof(Index), new
            {
                categoryId,
                stationId,
                showInactive
            });
        }

        var productsQuery = _db.Products
            .Where(p =>
                p.CategoryId == lpgCategory.Id &&
                p.StationId == stationId.Value);

        if (!showInactive)
        {
            productsQuery = productsQuery
                .Where(p => p.IsActive);
        }

        var products = await productsQuery.ToListAsync();

        if (products.Count == 0)
        {
            TempData["Err"] =
                "No LPG refill products were found for the selected station.";

            return RedirectToAction(nameof(Index), new
            {
                categoryId,
                stationId,
                showInactive
            });
        }

        var updatedCount = 0;
        var skippedCount = 0;

        foreach (var product in products)
        {
            if (!product.EquivalentKg.HasValue ||
                product.EquivalentKg.Value <= 0)
            {
                skippedCount++;
                continue;
            }

            product.DefaultUnitPrice = Math.Round(
                product.EquivalentKg.Value * pricePerKg,
                2,
                MidpointRounding.AwayFromZero);

            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        TempData["Ok"] =
            $"LPG refill prices updated. " +
            $"Updated {updatedCount} product(s), " +
            $"skipped {skippedCount}.";

        return RedirectToAction(nameof(Index), new
        {
            categoryId,
            stationId,
            showInactive
        });
    }
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(Product model)
    {
        model.Name = (model.Name ?? "").Trim();
        model.Sku = string.IsNullOrWhiteSpace(model.Sku) ? null : model.Sku.Trim();
        model.UnitType = "Piece";
    }

    private async Task ValidateReferencesAsync(Product model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "Product name is required.");

        if (model.CategoryId == Guid.Empty ||
            !await _db.ProductCategories.AnyAsync(c => c.Id == model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Please select a valid category.");
        }

        if (model.StationId == Guid.Empty ||
            !await _db.Stations.AnyAsync(s => s.Id == model.StationId))
        {
            ModelState.AddModelError(nameof(model.StationId), "Please select a valid station.");
        }
    }

    private async Task LoadLookupsAsync(
        Guid? selectedCategoryId = null,
        Guid? selectedStationId = null)
    {
        var categories = await _db.ProductCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var stations = await _db.Stations
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategoryId);
        ViewBag.Stations = new SelectList(stations, "Id", "Name", selectedStationId);
    }
}
