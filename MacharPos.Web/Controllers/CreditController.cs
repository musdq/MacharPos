using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.Credit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class CreditController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationCtx;
    private readonly UserManager<ApplicationUser> _users;

    public CreditController(AppDbContext db, IStationContext stationCtx, UserManager<ApplicationUser> users)
    {
        _db = db;
        _stationCtx = stationCtx;
        _users = users;
    }

    // Ledger: open credits first, filter by customer and date range (optional)
    [HttpGet]
    public async Task<IActionResult> Index(Guid? customerId = null, bool showSettled = false)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var q = _db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Where(s => s.StationId == stationId && s.IsCreditSale);

        if (!showSettled)
            q = q.Where(s => s.CreditBalance > 0);

        if (customerId.HasValue && customerId.Value != Guid.Empty)
            q = q.Where(s => s.CustomerId == customerId.Value);

        var sales = await q.OrderByDescending(s => s.CreatedAt).ToListAsync();

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.StationId == stationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Customers = customers.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == customerId)).ToList();
        ViewBag.ShowSettled = showSettled;

        return View(sales);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> New()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.StationId == stationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var walkin = customers.FirstOrDefault(c => c.Name == "Walk-in");

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var vm = new CreditSaleCreateVm
        {
            CustomerId = walkin?.Id,
            Customers = customers.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == walkin?.Id)).ToList(),
            Products = products.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList(),
            PaymentMethods = Enum.GetValues<PaymentMethod>()
                .Where(pm => pm != PaymentMethod.Credit) // credit isn't a payment-in method
                .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == PaymentMethod.Cash))
                .ToList(),
            Lines = new List<CreditSaleLineVm> { new CreditSaleLineVm() }
        };

        // price map for UI display
        ViewBag.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.DefaultUnitPrice);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CreditSaleCreateVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        // Reload dropdowns
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
            .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == vm.InitialPaymentMethod))
            .ToList();

        ViewBag.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.DefaultUnitPrice);

        vm.Lines = vm.Lines?.Where(l => l.ProductId != Guid.Empty && l.Qty > 0).ToList() ?? new();
        if (vm.Lines.Count == 0)
            ModelState.AddModelError("", "Add at least one item.");

        if (vm.InitialPaymentAmount < 0)
            ModelState.AddModelError("", "Initial payment cannot be negative.");

        if (!ModelState.IsValid) return View(vm);

        // Build sale (unit price enforced from DB)
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

        var sale = new Sale
        {
            StationId = stationId,
            CustomerId = vm.CustomerId,
            SoldByUserId = user.Id,
            PaymentMethod = PaymentMethod.Credit,
            CreatedAt = DateTime.UtcNow,
            ReceiptNo = $"CR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            IsCreditSale = true,
            CreditDueDate = vm.DueDate?.Date,
            CreditNotes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim()
        };

        decimal subtotal = 0m;
        decimal totalLpgKg = 0m;

        foreach (var line in vm.Lines)
        {
            if (!prodMap.TryGetValue(line.ProductId, out var p))
                continue;

            var qty = line.Qty;
            var unitPrice = p.DefaultUnitPrice; // enforce
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
                totalLpgKg += lpgKg;
            }

            subtotal += lineTotal;
            sale.Items.Add(item);
        }

        sale.Subtotal = subtotal;
        sale.DiscountAmount = 0m;
        sale.Total = subtotal;

        // LPG stock check
        if (activeBatch is not null && totalLpgKg > 0m)
        {
            if (activeBatch.RemainingKg < totalLpgKg)
            {
                ModelState.AddModelError("", $"Not enough LPG in active batch. Remaining {activeBatch.RemainingKg:N0}kg, attempted {totalLpgKg:N0}kg.");
                return View(vm);
            }
        }

        // credit balances
        var initialPaid = vm.InitialPaymentAmount;
        if (initialPaid > sale.Total) initialPaid = sale.Total;

        sale.CreditAmountPaid = initialPaid;
        sale.CreditBalance = sale.Total - initialPaid;
        sale.CreditStatus = sale.CreditBalance <= 0 ? CreditStatus.Settled : CreditStatus.Open;

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            // apply LPG sold to batch
            if (activeBatch is not null && totalLpgKg > 0m)
            {
                activeBatch.SoldKg += totalLpgKg;
                await _db.SaveChangesAsync();
            }

            // initial payment record
            if (initialPaid > 0)
            {
                _db.CreditPayments.Add(new CreditPayment
                {
                    SaleId = sale.Id,
                    Amount = initialPaid,
                    Method = vm.InitialPaymentMethod,
                    ReceivedAtUtc = DateTime.UtcNow,
                    ReceivedByUserId = user.Id,
                    Note = "Initial payment"
                });
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
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId && s.IsCreditSale);

        if (sale is null) return NotFound();

        return View(sale);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> AddPayment(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();

        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId && s.IsCreditSale);

        if (sale is null) return NotFound();

        var vm = new CreditPaymentVm
        {
            SaleId = sale.Id,
            ReceiptNo = sale.ReceiptNo,
            CustomerName = sale.Customer?.Name ?? "Walk-in",
            Total = sale.Total,
            AmountPaid = sale.CreditAmountPaid,
            Balance = sale.CreditBalance,
            PaymentMethods = Enum.GetValues<PaymentMethod>()
                .Where(pm => pm != PaymentMethod.Credit)
                .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == PaymentMethod.Cash))
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(CreditPaymentVm vm)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == vm.SaleId && s.StationId == stationId && s.IsCreditSale);
        if (sale is null) return NotFound();

        if (sale.CreditBalance <= 0)
        {
            TempData["Err"] = "This credit sale is already settled.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            vm.PaymentMethods = Enum.GetValues<PaymentMethod>()
                .Where(pm => pm != PaymentMethod.Credit)
                .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == vm.Method))
                .ToList();
            return View(vm);
        }

        var amount = vm.Amount;
        if (amount > sale.CreditBalance) amount = sale.CreditBalance;

        sale.CreditAmountPaid += amount;
        sale.CreditBalance = sale.Total - sale.CreditAmountPaid;
        sale.CreditStatus = sale.CreditBalance <= 0 ? CreditStatus.Settled : CreditStatus.Open;

        _db.CreditPayments.Add(new CreditPayment
        {
            SaleId = sale.Id,
            Amount = amount,
            Method = vm.Method,
            ReceivedAtUtc = DateTime.UtcNow,
            ReceivedByUserId = user.Id,
            Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim()
        });

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Payment added.";
        return RedirectToAction(nameof(Index), new { customerId = sale.CustomerId });
    }
}