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
    public async Task<IActionResult> Index(Guid? categoryId = null, bool showInactive = false)
    {
        var cats = await _db.ProductCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewBag.Categories = new SelectList(cats, "Id", "Name", categoryId);
        ViewBag.CategoryId = categoryId;
        ViewBag.ShowInactive = showInactive;

        var q = _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);
        if (!showInactive) q = q.Where(p => p.IsActive);

        var products = await q.OrderBy(p => p.Category!.Name).ThenBy(p => p.Name).ToListAsync();
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync();
        return View(new Product { DefaultUnitPrice = 0m, UnitType = "Piece", IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model)
    {
        model.Name = (model.Name ?? "").Trim();
        model.Sku = string.IsNullOrWhiteSpace(model.Sku) ? null : model.Sku.Trim();
        model.UnitType = "Piece";

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Name))
        {
            await LoadCategoriesAsync(model.CategoryId);
            ModelState.AddModelError("", "Please fill required fields.");
            return View(model);
        }

        _db.Products.Add(model);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Product created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        await LoadCategoriesAsync(p.CategoryId);
        return View(p);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Product model)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        p.Name = (model.Name ?? "").Trim();
        p.Sku = string.IsNullOrWhiteSpace(model.Sku) ? null : model.Sku.Trim();
        p.CategoryId = model.CategoryId;
        p.DefaultUnitPrice = model.DefaultUnitPrice;
        p.EquivalentKg = model.EquivalentKg;
        p.IsActive = model.IsActive;
        p.UnitType = "Piece";

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoriesAsync(Guid? selected = null)
    {
        var cats = await _db.ProductCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(cats, "Id", "Name", selected);
    }
}