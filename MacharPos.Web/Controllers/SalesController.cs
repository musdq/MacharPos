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
using System;
using System.Linq;

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


    // Normalizes date for DATE column ("date") => Kind must NOT be Utc
    private static DateTime AsDateOnly(DateTime dt)
        => DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);

    

    private bool IsAdminOverride()
        => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private async Task<(bool Allowed, string Message)> GuardClosedDayAsync(Guid stationId, DateTime businessDate)
    {
        if (IsAdminOverride())
            return (true, "");

        var dateOnly = AsDateOnly(businessDate);

        var isClosed = await _db.DailyCloses.AsNoTracking()
            .AnyAsync(x => x.StationId == stationId && x.CloseDate == dateOnly && x.IsClosed);

        if (!isClosed) return (true, "");

        return (false, $"Operation not allowed. {dateOnly:yyyy-MM-dd} is already closed. Admin only.");
    }

private static string MakeStationCode(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName)) return "STN";
        // Keep letters/numbers only, take first 3 chars
        var cleaned = new string(stationName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleaned.Length >= 3) return cleaned[..3];
        return cleaned.PadRight(3, 'X');
    }


    [HttpGet]
    public async Task<IActionResult> Index(string? quick = null, DateTime? startDate = null, DateTime? endDate = null, Guid? stationId = null)
    {
        Guid? stationIdEffective = null;
        if (IsAdminOverride())
        {
            stationIdEffective = stationId;
        }
        else
        {
            stationIdEffective = await _stationCtx.GetStationIdOrThrowAsync();
        }

        Station? station = null;
        if (stationIdEffective.HasValue)
        {
            station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationIdEffective.Value);
        }
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
            .Include(s => s.Station)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .Where(s => (!stationIdEffective.HasValue || s.StationId == stationIdEffective.Value) && s.CreatedAt >= utcStart && s.CreatedAt < utcEndExclusive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        ViewBag.StationName = station?.Name ?? "All Stations";
        ViewBag.StationId = stationIdEffective?.ToString();
        if (IsAdminOverride())
        {
            ViewBag.Stations = await _db.Stations.AsNoTracking().OrderBy(s => s.Name)
                .Select(s => new SelectListItem(s.Name, s.Id.ToString(), stationIdEffective.HasValue && s.Id == stationIdEffective.Value))
                .ToListAsync();
        }
        ViewBag.Quick = quick;
        ViewBag.StartDate = utcStart.ToString("yyyy-MM-dd");
        ViewBag.EndDate = utcEndExclusive.AddDays(-1).ToString("yyyy-MM-dd");

        ViewBag.Total = sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Total);

        return View(sales);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
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

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(Guid id, string? reason)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        reason = (reason ?? "").Trim();
        if (reason.Length < 3)
        {
            TempData["Err"] = "Void reason is required.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var sale = await _db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId);

        if (sale is null) return NotFound();

        var guard = await GuardClosedDayAsync(stationId, sale.CreatedAt);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction(nameof(Details), new { id });
        }


        if (sale.Status == SaleStatus.Voided)
        {
            TempData["Err"] = "Sale is already voided.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Do not allow voiding credit sales once payments have been received.
        var hasPayments = await _db.CreditPayments.AnyAsync(p => p.SaleId == sale.Id);
        if (hasPayments)
        {
            TempData["Err"] = "Cannot void: this sale has credit payment records. Use a proper refund/adjustment flow.";
            return RedirectToAction(nameof(Details), new { id });
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Reverse LPG batch sold Kg if needed
            var lpgByBatch = sale.Items
                .Where(i => i.LpgBatchId.HasValue && (i.LpgKg ?? 0m) > 0m)
                .GroupBy(i => i.LpgBatchId!.Value)
                .Select(g => new { BatchId = g.Key, Kg = g.Sum(x => x.LpgKg ?? 0m) })
                .ToList();

            if (lpgByBatch.Count > 0)
            {
                var batchIds = lpgByBatch.Select(x => x.BatchId).ToList();
                var batches = await _db.LpgBatches
                    .Where(b => b.StationId == stationId && batchIds.Contains(b.Id))
                    .ToListAsync();


                foreach (var grp in lpgByBatch)
                {
                    var batch = batches.FirstOrDefault(b => b.Id == grp.BatchId);
                    if (batch is null) continue;
                    batch.SoldKg = Math.Max(0m, batch.SoldKg - grp.Kg);
                }
                await _db.SaveChangesAsync();
            }

            sale.Status = SaleStatus.Voided;
            sale.VoidedByUserId = user.Id;
            sale.VoidedAtUtc = DateTime.UtcNow;
            sale.VoidReason = reason;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Ok"] = "Sale voided.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> New()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        

        var guard = await GuardClosedDayAsync(stationId, DateTime.Today);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction("Index", "DailyClose", new { date = AsDateOnly(DateTime.Today).ToString("yyyy-MM-dd") });
        }
// Phase 1 control: warn if previous business day wasn't closed
        var yesterdayDateOnly = AsDateOnly(DateTime.Today.AddDays(-1));

        // Only warn if this station has any prior activity (sales before today)
        var hasPriorActivity = await _db.Sales.AsNoTracking()
            .AnyAsync(s => s.StationId == stationId && s.CreatedAt < DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc));

        if (hasPriorActivity)
        {
            var yesterdayClosed = await _db.DailyCloses.AsNoTracking()
                .AnyAsync(x => x.StationId == stationId && x.CloseDate == yesterdayDateOnly && x.IsClosed);

            if (!yesterdayClosed)
            {
                ViewBag.UnclosedDate = yesterdayDateOnly.ToString("yyyy-MM-dd");
                ViewBag.UnclosedDateDisplay = yesterdayDateOnly.ToString("ddd, dd MMM yyyy");
            }
        }

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
            PaymentMethods = Enum.GetValues<PaymentMethod>().Where(pm => pm != PaymentMethod.Credit)
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


        var guard = await GuardClosedDayAsync(stationId, DateTime.Today);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction("Index", "DailyClose", new { date = AsDateOnly(DateTime.Today).ToString("yyyy-MM-dd") });
        }
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        await PopulateDropdownsAsync(vm, stationId);

        NormalizeAndValidateLines(vm);
        if (!ModelState.IsValid) return View(vm);

        var prodMap = await LoadProductsMapAsync(vm);

        // Determine if we need LPG batch + if we need manager approval
        var needsLpgBatch = vm.Lines.Any(l => prodMap.TryGetValue(l.ProductId, out var p) && (p.EquivalentKg ?? 0m) > 0m);
        var hasPriceOverride = HasPriceOverride(vm, prodMap);
        var discount = NormalizeDiscount(vm.DiscountAmount);

        if (discount > 0 && discount > CalculateSubtotalPreview(vm, prodMap))
        {
            ModelState.AddModelError("", "Discount cannot be greater than subtotal.");
            return View(vm);
        }

        // ✅ FIX: declare approvedByUserId BEFORE it’s used
        string? approvedByUserId = null;

        if (discount > 0 || hasPriceOverride)
        {
            var approval = await TryGetManagerApprovalAsync(vm, stationId);
            if (!approval.Success)
            {
                ModelState.AddModelError("", approval.ErrorMessage ?? "Manager authorization failed.");
                return View(vm);
            }
            approvedByUserId = approval.ManagerUserId;
        }

        var activeBatch = needsLpgBatch
            ? await GetActiveLpgBatchOrModelErrorAsync(stationId)
            : null;

        if (needsLpgBatch && activeBatch is null)
            return View(vm); // error already added

        // Save transactionally (includes receipt sequence lock)
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var receiptNo = await GenerateReceiptNoAsync(stationId);

            // Build sale + items
            var sale = BuildSaleSkeleton(vm, stationId, user.Id, receiptNo);

            var build = BuildSaleItems(vm, prodMap, activeBatch, approvedByUserId);
            if (!build.Success)
            {
                ModelState.AddModelError("", build.ErrorMessage ?? "Unable to build sale items.");
                return View(vm);
            }

            sale.Items.AddRange(build.Items);
            sale.Subtotal = build.Subtotal;
            sale.DiscountAmount = discount;
            sale.Total = build.Subtotal - discount;
            sale.DiscountApprovedByUserId = approvedByUserId;
            sale.DiscountApprovedAt = (discount > 0) ? DateTime.UtcNow : null;

            // Stock validation for LPG batch
            if (activeBatch is not null && build.TotalLpgKg > 0m && activeBatch.RemainingKg < build.TotalLpgKg)
            {
                ModelState.AddModelError("",
                    $"Not enough LPG in active batch. Remaining {activeBatch.RemainingKg:N0}kg, attempted {build.TotalLpgKg:N0}kg.");
                return View(vm);
            }

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            if (activeBatch is not null && build.TotalLpgKg > 0m)
            {
                activeBatch.SoldKg += build.TotalLpgKg;
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return RedirectToAction(nameof(Receipt), new { id = sale.Id });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task PopulateDropdownsAsync(SaleCreateVm vm, Guid stationId)
    {
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
            .Where(pm => pm != PaymentMethod.Credit)
            .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == vm.PaymentMethod))
            .ToList();
    }

    private void NormalizeAndValidateLines(SaleCreateVm vm)
    {
        vm.Lines = vm.Lines?
            .Where(l => l.ProductId != Guid.Empty && l.Qty > 0)
            .ToList() ?? new List<SaleLineVm>();

        if (vm.Lines.Count == 0)
            ModelState.AddModelError("", "Add at least one valid item.");
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsMapAsync(SaleCreateVm vm)
    {
        var productIds = vm.Lines.Select(l => l.ProductId).Distinct().ToList();
        return await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
    }

    private bool HasPriceOverride(SaleCreateVm vm, Dictionary<Guid, Product> prodMap)
    {
        foreach (var l in vm.Lines)
        {
            if (!prodMap.TryGetValue(l.ProductId, out var prod)) continue;
            if (l.UnitPrice > 0m && l.UnitPrice != prod.DefaultUnitPrice) return true;
        }
        return false;
    }

    private decimal NormalizeDiscount(decimal discount)
    {
        return discount < 0 ? 0 : discount;
    }

    // Used only for discount sanity-check before building items
    private decimal CalculateSubtotalPreview(SaleCreateVm vm, Dictionary<Guid, Product> prodMap)
    {
        decimal subtotal = 0m;
        foreach (var l in vm.Lines)
        {
            if (!prodMap.TryGetValue(l.ProductId, out var p)) continue;
            var unitPrice = (l.UnitPrice > 0m) ? l.UnitPrice : p.DefaultUnitPrice;
            subtotal += l.Qty * unitPrice;
        }
        return subtotal;
    }

    private async Task<LpgBatch?> GetActiveLpgBatchOrModelErrorAsync(Guid stationId)
    {
        var batch = await _db.LpgBatches
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        if (batch is null)
            ModelState.AddModelError("", "No ACTIVE LPG batch for your station. Create and activate a batch before LPG refill sales.");

        return batch;
    }

    private async Task<string> GenerateReceiptNoAsync(Guid stationId)
    {

        var station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationId);
        var stationCode = MakeStationCode(station.Name);

        // Receipt numbering is per station per UTC day.
        // To avoid duplicates under concurrency, we serialize receipt generation using a PostgreSQL advisory transaction lock.
        var utcDayStart = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var utcDayEnd = utcDayStart.AddDays(1);

        // Stable per-station key (avoid Guid.GetHashCode which is process-random)
        var stationKey = BitConverter.ToInt32(stationId.ToByteArray(), 0);
        var dayKey = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));

        // Holds until the surrounding DB transaction commits/rolls back
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1});", stationKey, dayKey);

        var todayCount = await _db.Sales.AsNoTracking()
            .Where(s => s.StationId == stationId && s.CreatedAt >= utcDayStart && s.CreatedAt < utcDayEnd)
            .CountAsync();

        var receiptSeq = todayCount + 1;
        return $"{stationCode}-{utcDayStart:yyyyMMdd}-{receiptSeq:000000}";

    }

    private Sale BuildSaleSkeleton(SaleCreateVm vm, Guid stationId, string soldByUserId, string receiptNo)
    {
        return new Sale
        {
            StationId = stationId,
            CustomerId = vm.CustomerId,
            SoldByUserId = soldByUserId,
            PaymentMethod = vm.PaymentMethod,
            CreatedAt = DateTime.UtcNow,
            ReceiptNo = receiptNo,
            Items = new List<SaleItem>()
        };
    }

    private (bool Success, string? ErrorMessage, List<SaleItem> Items, decimal Subtotal, decimal TotalLpgKg)
        BuildSaleItems(
            SaleCreateVm vm,
            Dictionary<Guid, Product> prodMap,
            LpgBatch? activeBatch,
            string? approvedByUserId)
    {
        var items = new List<SaleItem>();
        decimal subtotal = 0m;
        decimal totalLpgKg = 0m;

        foreach (var line in vm.Lines)
        {
            if (!prodMap.TryGetValue(line.ProductId, out var p))
                continue;

            var qty = line.Qty;

            var wantsOverride = line.UnitPrice > 0m && line.UnitPrice != p.DefaultUnitPrice;
            var unitPrice = p.DefaultUnitPrice;

            if (wantsOverride)
            {
                if (approvedByUserId is null)
                    return (false, "Manager authorization is required for price override.", new(), 0m, 0m);

                unitPrice = line.UnitPrice;
            }

            var lineTotal = qty * unitPrice;

            var item = new SaleItem
            {
                ProductId = p.Id,
                Qty = qty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            };

            var eqKg = p.EquivalentKg ?? 0m;
            if (eqKg > 0m && activeBatch is not null)
            {
                var lpgKg = qty * eqKg;
                item.LpgBatchId = activeBatch.Id;
                item.LpgKg = lpgKg;

                // Phase 6.2: stamp LPG cost at time of sale (for stable margins later)
                if (activeBatch.CostPerKg.HasValue)
                {
                    item.UnitCostAtSale = activeBatch.CostPerKg.Value;
                    item.TotalCostAtSale = lpgKg * activeBatch.CostPerKg.Value;
                }
                totalLpgKg += lpgKg;
            }

            subtotal += lineTotal;
            items.Add(item);
        }

        if (items.Count == 0)
            return (false, "Add at least one valid item.", new(), 0m, 0m);

        return (true, null, items, subtotal, totalLpgKg);
    }

    private async Task<(bool Success, string? ErrorMessage, string? ManagerUserId)> TryGetManagerApprovalAsync(SaleCreateVm vm, Guid stationId)
    {
        if (string.IsNullOrWhiteSpace(vm.ManagerEmail) || string.IsNullOrWhiteSpace(vm.ManagerPassword))
            return (false, "Manager authorization is required for discount or price override.", null);

        var mgr = await _users.FindByEmailAsync(vm.ManagerEmail.Trim());
        if (mgr is null) return (false, "Invalid manager credentials.", null);

        var pwOk = await _signIn.CheckPasswordSignInAsync(mgr, vm.ManagerPassword, lockoutOnFailure: false);
        if (!pwOk.Succeeded) return (false, "Invalid manager credentials.", null);

        var isManager = await _users.IsInRoleAsync(mgr, "StationManager") || await _users.IsInRoleAsync(mgr, "Admin");
        if (!isManager) return (false, "Only StationManager/Admin can approve discounts.", null);

        if (mgr.HomeStationId != stationId && !await _users.IsInRoleAsync(mgr, "Admin"))
            return (false, "Manager must belong to this station to approve discount.", null);

        return (true, null, mgr.Id);
    }


    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Station)
            .Include(s => s.Station)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId);

        if (sale is null) return NotFound();

        return View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? quick = null, DateTime? startDate = null, DateTime? endDate = null, Guid? stationId = null)
    {
        Guid? stationIdEffective = null;
        if (IsAdminOverride())
        {
            stationIdEffective = stationId;
        }
        else
        {
            stationIdEffective = await _stationCtx.GetStationIdOrThrowAsync();
        }

        Station? station = null;
        if (stationIdEffective.HasValue)
        {
            station = await _db.Stations.AsNoTracking().FirstAsync(s => s.Id == stationIdEffective.Value);
        }
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
            .Include(s => s.Station)
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => (!stationIdEffective.HasValue || s.StationId == stationIdEffective.Value) && s.CreatedAt >= utcStart && s.CreatedAt < utcEndExclusive)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sales");

        ws.Cell(1, 1).Value = "Station";
        ws.Cell(1, 2).Value = "ReceiptNo";
        ws.Cell(1, 3).Value = "DateTime(UTC)";
        ws.Cell(1, 4).Value = "Customer";
        ws.Cell(1, 5).Value = "PaymentMethod";
        ws.Cell(1, 6).Value = "Status";
        ws.Cell(1, 7).Value = "Item";
        ws.Cell(1, 8).Value = "Qty";
        ws.Cell(1, 9).Value = "UnitPrice";
        ws.Cell(1, 10).Value = "LineTotal";
        ws.Cell(1, 11).Value = "LpgKg";
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var s in sales)
        {
            foreach (var it in s.Items)
            {
                ws.Cell(r, 1).Value = s.Station?.Name ?? (station?.Name ?? "");
                ws.Cell(r, 2).Value = s.ReceiptNo;
                ws.Cell(r, 3).Value = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                ws.Cell(r, 4).Value = s.Customer?.Name ?? "Walk-in";
                ws.Cell(r, 5).Value = s.PaymentMethod.ToString();
                ws.Cell(r, 6).Value = s.Status.ToString();
                ws.Cell(r, 7).Value = it.Product?.Name ?? "";
                ws.Cell(r, 8).Value = it.Qty;
                ws.Cell(r, 9).Value = it.UnitPrice;
                ws.Cell(r, 10).Value = it.LineTotal;
                ws.Cell(r, 11).Value = it.LpgKg ?? 0m;
                r++;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var stationPart = station?.Name.Replace(" ", "_") ?? "All_Stations";
        var fileName = $"Sales_{stationPart}_{utcStart:yyyyMMdd}_{utcEndExclusive.AddDays(-1):yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}