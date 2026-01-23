using MacharPos.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MacharPos.Core.Entities;

namespace MacharPos.Web.Controllers;

[Authorize(Roles = "Admin,StationManager")]
public class ProductCategoriesController : Controller
{
    private readonly AppDbContext _db;
    public ProductCategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cats = await _db.ProductCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(cats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Err"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _db.ProductCategories.AnyAsync(x => x.Name.ToLower() == name.ToLower());
        if (exists)
        {
            TempData["Err"] = "Category already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.ProductCategories.Add(new ProductCategory { Name = name, IsActive = true });
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var cat = await _db.ProductCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (cat is null) return NotFound();

        cat.IsActive = !cat.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}