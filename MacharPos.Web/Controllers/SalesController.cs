using ClosedXML.Excel;
using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class SalesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public SalesController(AppDbContext db, IStationContext stationCtx, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _db = db;
        _stationCtx = stationCtx;
        _users = users;
        _signIn = signIn;

    }

    [HttpGet]
    public async Task<IActionResult> Index(string? quick = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        // Determine range (UTC)
        DateTime utcStart;
        DateTime utcEndExclusive;

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(quick))
        {
            quick = quick.ToLowerInvariant();
            if (quick == "today")
            {
                utcStart = now.Date;
                utcEndExclusive = utcStart.AddDays(1);
            }
            else if (quick == "week")
            {
                // last 7 days
                utcEndExclusive = now.Date.AddDays(1);
                utcStart = utcEndExclusive.AddDays(-7);
            }
            else if (quick == "month")
            {
                utcStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                utcEndExclusive = utcStart.AddMonths(1);
            }
            else
            {
                utcStart = now.Date;
                utcEndExclusive = utcStart.AddDays(1);
            }
        }
        else
        {
            var s = (startDate?.Date ?? now.Date);
            var e = (endDate?.Date ?? s);

            utcStart = DateTime.SpecifyKind(s, DateTimeKind.Utc);
            utcEndExclusive = DateTime.SpecifyKind(e.AddDays(1), DateTimeKind.Utc);

            if (utcEndExclusive < utcStart.AddDays(1))
                utcEndExclusive = utcStart.AddDays(1);
        }

        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcStart && s.CreatedAt < utcEndExclusive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        ViewBag.StationName = station.Name;
        ViewBag.Quick = quick;
        ViewBag.StartDate = utcStart.ToString("yyyy-MM-dd");
        ViewBag.EndDate = utcEndExclusive.AddDays(-1).ToString("yyyy-MM-dd");

        ViewBag.Total = sales.Sum(s => s.Total);

        return View(sales);
    }

    [HttpGet]
    public async Task<IActionResult> New()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        // Customers for station, default Walk-in
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.StationId == stationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var walkin = customers.FirstOrDefault(c => c.Name == "Walk-in");

        // Products (active only)
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.DefaultUnitPrice);


        var vm = new SaleCreateVm
        {
            CustomerId = walkin?.Id,
            Customers = customers.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == walkin?.Id)).ToList(),
            Products = products.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList(),
            PaymentMethods = Enum.GetValues<PaymentMethod>()
                .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == PaymentMethod.Cash))
                .ToList(),
            Lines = new List<SaleLineVm> { new SaleLineVm() }
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(SaleCreateVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        // Reload dropdowns (in case of validation error)
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.StationId == stationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        vm.Customers = customers.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == vm.CustomerId)).ToList();
        vm.Products = products.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();
        vm.PaymentMethods = Enum.GetValues<PaymentMethod>()
            .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == vm.PaymentMethod))
            .ToList();

        if (vm.Lines is null || vm.Lines.Count == 0)
            ModelState.AddModelError("", "Add at least one item.");

        // Basic line validation
        vm.Lines = vm.Lines?.Where(l => l.ProductId != Guid.Empty && l.Qty > 0).ToList() ?? new();
        if (vm.Lines.Count == 0)
            ModelState.AddModelError("", "Add at least one valid item.");

        if (!ModelState.IsValid)
            return View(vm);

        // Load product details for calculations
        var productIds = vm.Lines.Select(l => l.ProductId).Distinct().ToList();
        var prodMap = await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        // Find active LPG batch if needed
        var needsLpgBatch = vm.Lines.Any(l => prodMap.TryGetValue(l.ProductId, out var p) && (p.EquivalentKg ?? 0m) > 0m);
        LpgBatch? activeBatch = null;
        if (needsLpgBatch)
        {
            activeBatch = await _db.LpgBatches.FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);
            if (activeBatch is null)
            {
                ModelState.AddModelError("", "No ACTIVE LPG batch for your station. Create and activate a batch before LPG refill sales.");
                return View(vm);
            }
        }

        // Build sale + items
        var sale = new Sale
        {
            StationId = stationId,
            CustomerId = vm.CustomerId,
            SoldByUserId = user.Id,
            PaymentMethod = vm.PaymentMethod,
            CreatedAt = DateTime.UtcNow,
            ReceiptNo = $"RCPT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
        };

        decimal subtotal = 0m;
        decimal totalLpgKg = 0m;

        foreach (var line in vm.Lines)
        {
            if (!prodMap.TryGetValue(line.ProductId, out var p))
                continue;

            var qty = line.Qty;
            var unitPrice = p.DefaultUnitPrice;
            var lineTotal = qty * unitPrice;

            var item = new SaleItem
            {
                ProductId = p.Id,
                Qty = qty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            };

            // LPG linkage
            var eqKg = p.EquivalentKg ?? 0m;
            if (eqKg > 0m && activeBatch is not null)
            {
                var lpgKg = qty * eqKg;
                item.LpgBatchId = activeBatch.Id;
                item.LpgKg = lpgKg;
                totalLpgKg += lpgKg;
            }

            subtotal += lineTotal;
            sale.Items.Add(item);
        }

        var discount = vm.DiscountAmount;

        if (discount < 0) discount = 0;
        if (discount > subtotal)
        {
            ModelState.AddModelError("", "Discount cannot be greater than subtotal.");
            return View(vm);
        }

        string? approvedByUserId = null;

        if (discount > 0)
        {
            if (string.IsNullOrWhiteSpace(vm.ManagerEmail) || string.IsNullOrWhiteSpace(vm.ManagerPassword))
            {
                ModelState.AddModelError("", "Manager authorization is required for discount.");
                return View(vm);
            }

            var mgr = await _users.FindByEmailAsync(vm.ManagerEmail.Trim());
            if (mgr is null)
            {
                ModelState.AddModelError("", "Invalid manager credentials.");
                return View(vm);
            }

            var pwOk = await _signIn.CheckPasswordSignInAsync(mgr, vm.ManagerPassword, lockoutOnFailure: false);
            if (!pwOk.Succeeded)
            {
                ModelState.AddModelError("", "Invalid manager credentials.");
                return View(vm);
            }

            var isManager = await _users.IsInRoleAsync(mgr, "StationManager") || await _users.IsInRoleAsync(mgr, "Admin");
            if (!isManager)
            {
                ModelState.AddModelError("", "Only StationManager/Admin can approve discounts.");
                return View(vm);
            }

            // Optional: enforce same station
            if (mgr.HomeStationId != stationId && !await _users.IsInRoleAsync(mgr, "Admin"))
            {
                ModelState.AddModelError("", "Manager must belong to this station to approve discount.");
                return View(vm);
            }

            approvedByUserId = mgr.Id;
        }


        sale.Subtotal = subtotal;
        sale.DiscountAmount = discount;
        sale.Total = subtotal - discount;
        sale.DiscountApprovedByUserId = approvedByUserId;
        sale.DiscountApprovedAt = (discount > 0) ? DateTime.UtcNow : null;


        // Stock validation for LPG batch
        if (activeBatch is not null && totalLpgKg > 0m)
        {
            if (activeBatch.RemainingKg < totalLpgKg)
            {
                ModelState.AddModelError("", $"Not enough LPG in active batch. Remaining {activeBatch.RemainingKg:N0}kg, attempted {totalLpgKg:N0}kg.");
                return View(vm);
            }
        }


        // Save transactionally
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            if (activeBatch is not null && totalLpgKg > 0m)
            {
                activeBatch.SoldKg += totalLpgKg;
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return RedirectToAction(nameof(Receipt), new { id = sale.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Station)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId);

        if (sale is null) return NotFound();

        return View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? quick = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);

        DateTime utcStart;
        DateTime utcEndExclusive;
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(quick))
        {
            quick = quick.ToLowerInvariant();
            if (quick == "today")
            {
                utcStart = now.Date;
                utcEndExclusive = utcStart.AddDays(1);
            }
            else if (quick == "week")
            {
                utcEndExclusive = now.Date.AddDays(1);
                utcStart = utcEndExclusive.AddDays(-7);
            }
            else if (quick == "month")
            {
                utcStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                utcEndExclusive = utcStart.AddMonths(1);
            }
            else
            {
                utcStart = now.Date;
                utcEndExclusive = utcStart.AddDays(1);
            }
        }
        else
        {
            var s = (startDate?.Date ?? now.Date);
            var e = (endDate?.Date ?? s);

            // IMPORTANT: mark as UTC so Npgsql accepts it for timestamptz comparisons
            utcStart = DateTime.SpecifyKind(s, DateTimeKind.Utc);
            utcEndExclusive = DateTime.SpecifyKind(e.AddDays(1), DateTimeKind.Utc);

            if (utcEndExclusive < utcStart.AddDays(1))
                utcEndExclusive = utcStart.AddDays(1);
        }

        var sales = await _db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcStart && s.CreatedAt < utcEndExclusive)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sales");

        ws.Cell(1, 1).Value = "Station";
        ws.Cell(1, 2).Value = "ReceiptNo";
        ws.Cell(1, 3).Value = "DateTime(UTC)";
        ws.Cell(1, 4).Value = "Customer";
        ws.Cell(1, 5).Value = "PaymentMethod";
        ws.Cell(1, 6).Value = "Item";
        ws.Cell(1, 7).Value = "Qty";
        ws.Cell(1, 8).Value = "UnitPrice";
        ws.Cell(1, 9).Value = "LineTotal";
        ws.Cell(1, 10).Value = "LpgKg";
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var s in sales)
        {
            foreach (var it in s.Items)
            {
                ws.Cell(r, 1).Value = station.Name;
                ws.Cell(r, 2).Value = s.ReceiptNo;
                ws.Cell(r, 3).Value = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cell(r, 4).Value = s.Customer?.Name ?? "Walk-in";
                ws.Cell(r, 5).Value = s.PaymentMethod.ToString();
                ws.Cell(r, 6).Value = it.Product?.Name ?? "";
                ws.Cell(r, 7).Value = it.Qty;
                ws.Cell(r, 8).Value = it.UnitPrice;
                ws.Cell(r, 9).Value = it.LineTotal;
                ws.Cell(r, 10).Value = it.LpgKg ?? 0m;
                r++;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"Sales_{station.Name.Replace(" ", "_")}_{utcStart:yyyyMMdd}_{utcEndExclusive.AddDays(-1):yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}