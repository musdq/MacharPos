using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.ViewModels.Credit;
using MacharPos.Web.ViewModels.Sales;
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

        var guard = await GuardClosedDayAsync(stationId, DateTime.Today);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction("Index", "DailyClose", new { date = AsDateOnly(DateTime.Today).ToString("yyyy-MM-dd") });
        }

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
            Customers = customers.Select(c => new SelectListItem(
                c.Name == "Walk-in"
                    ? "Walk-in"
                    : string.IsNullOrWhiteSpace(c.Phone)
                        ? c.Name
                        : $"{c.Name} — {c.Phone}",
                c.Id.ToString(),
                c.Id == walkin?.Id)).ToList(),
            Products = products.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList(),
            PaymentMethods = Enum.GetValues<PaymentMethod>()
                .Where(pm => pm != PaymentMethod.Credit)
                .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == PaymentMethod.Cash))
                .ToList(),
            Lines = new List<CreditSaleLineVm> { new CreditSaleLineVm { Qty = 1 } }
        };

        PopulateProductViewBags(products);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StationManager")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CreditSaleCreateVm vm)
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

        if (vm.InitialPaymentAmount < 0)
            ModelState.AddModelError("", "Initial payment cannot be negative.");

        if (!ModelState.IsValid) return View(vm);

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == vm.CustomerId && c.StationId == stationId && c.IsActive);

        if (customer is null)
        {
            ModelState.AddModelError("", "Invalid customer.");
            return View(vm);
        }

        if (!customer.IsCreditApproved)
        {
            ModelState.AddModelError("", "This customer is not approved for credit.");
            return View(vm);
        }

        var prodMap = await LoadProductsMapAsync(vm);

        var needsLpgBatch = vm.Lines.Any(l =>
            l.ProductId != Guid.Empty &&
            prodMap.TryGetValue(l.ProductId, out var p) &&
            (p.EquivalentKg ?? 0m) > 0m);

        var activeBatch = needsLpgBatch
            ? await GetActiveLpgBatchOrModelErrorAsync(stationId)
            : null;

        if (needsLpgBatch && activeBatch is null)
            return View(vm);

        var build = BuildCreditSaleItems(vm, prodMap, activeBatch);
        if (!build.Success)
        {
            ModelState.AddModelError("", build.ErrorMessage ?? "Unable to build credit sale items.");
            return View(vm);
        }

        if (activeBatch is not null && build.TotalLpgKg > 0m && activeBatch.RemainingKg < build.TotalLpgKg)
        {
            ModelState.AddModelError("",
                $"Not enough LPG in active batch. Remaining {activeBatch.RemainingKg:N2}kg, attempted {build.TotalLpgKg:N2}kg.");
            return View(vm);
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
            CreditDueDate = (vm.DueDate?.Date) ?? (customer.CreditDueDays > 0 ? DateTime.UtcNow.Date.AddDays(customer.CreditDueDays) : null),
            CreditNotes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
            Items = new List<SaleItem>()
        };

        sale.Items.AddRange(build.Items);
        sale.Subtotal = build.Subtotal;
        sale.DiscountAmount = 0m;
        sale.Total = build.Subtotal;

        var initialPaid = vm.InitialPaymentAmount;
        if (initialPaid > sale.Total) initialPaid = sale.Total;

        sale.CreditAmountPaid = initialPaid;
        sale.CreditBalance = sale.Total - initialPaid;
        sale.CreditStatus = sale.CreditBalance <= 0 ? CreditStatus.Settled : CreditStatus.Open;

        if (customer.CreditLimit > 0m && sale.CreditBalance > 0m)
        {
            var outstanding = await _db.Sales.AsNoTracking()
                .Where(s => s.StationId == stationId
                            && s.CustomerId == customer.Id
                            && s.IsCreditSale
                            && s.Status == SaleStatus.Completed)
                .Select(s => s.Total - (_db.CreditPayments.Where(p => p.SaleId == s.Id).Sum(p => (decimal?)p.Amount) ?? 0m))
                .Where(b => b > 0m)
                .SumAsync();

            if (outstanding + sale.CreditBalance > customer.CreditLimit)
            {
                ModelState.AddModelError("", $"Credit limit exceeded. Current outstanding: ₦{outstanding:N2}. This sale adds ₦{sale.CreditBalance:N2}. Limit: ₦{customer.CreditLimit:N2}.");
                return View(vm);
            }
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            if (activeBatch is not null && build.TotalLpgKg > 0m)
            {
                activeBatch.SoldKg += build.TotalLpgKg;
                await _db.SaveChangesAsync();
            }

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

    private async Task PopulateDropdownsAsync(CreditSaleCreateVm vm, Guid stationId)
    {
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.StationId == stationId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        vm.Customers = customers.Select(c => new SelectListItem(
            c.Name == "Walk-in"
                ? "Walk-in"
                : string.IsNullOrWhiteSpace(c.Phone)
                    ? c.Name
                    : $"{c.Name} — {c.Phone}",
            c.Id.ToString(),
            c.Id == vm.CustomerId)).ToList();

        vm.Products = products.Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();

        vm.PaymentMethods = Enum.GetValues<PaymentMethod>()
            .Where(pm => pm != PaymentMethod.Credit)
            .Select(pm => new SelectListItem(pm.ToString(), ((int)pm).ToString(), pm == vm.InitialPaymentMethod))
            .ToList();

        PopulateProductViewBags(products);
    }

    private void PopulateProductViewBags(List<Product> products)
    {
        ViewBag.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.DefaultUnitPrice);
        ViewBag.ProductEquivalentKgs = products.ToDictionary(p => p.Id.ToString(), p => p.EquivalentKg ?? 0m);
    }

    private void NormalizeAndValidateLines(CreditSaleCreateVm vm)
    {
        vm.Lines = vm.Lines?
            .Where(l => l.ProductId != Guid.Empty)
            .ToList() ?? new List<CreditSaleLineVm>();

        if (vm.Lines.Count == 0)
        {
            ModelState.AddModelError("", "Add at least one valid item.");
            return;
        }

        foreach (var line in vm.Lines)
        {
            if (line.Qty <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than zero.");
                continue;
            }

            if (line.EntryMode == SaleEntryMode.ByAmount)
            {
                if (!line.Amount.HasValue || line.Amount.Value <= 0)
                {
                    ModelState.AddModelError("", "Amount must be greater than zero.");
                    continue;
                }
            }
        }
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsMapAsync(CreditSaleCreateVm vm)
    {
        var productIds = vm.Lines.Select(l => l.ProductId).Distinct().ToList();

        return await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
    }

    private async Task<LpgBatch?> GetActiveLpgBatchOrModelErrorAsync(Guid stationId)
    {
        var batch = await _db.LpgBatches
            .FirstOrDefaultAsync(b => b.StationId == stationId && b.Status == LpgBatchStatus.Active);

        if (batch is null)
            ModelState.AddModelError("", "No ACTIVE LPG batch for your station. Create and activate a batch before LPG refill sales.");

        return batch;
    }

    private (bool Success, string? ErrorMessage, List<SaleItem> Items, decimal Subtotal, decimal TotalLpgKg)
        BuildCreditSaleItems(CreditSaleCreateVm vm, Dictionary<Guid, Product> prodMap, LpgBatch? activeBatch)
    {
        var items = new List<SaleItem>();
        decimal subtotal = 0m;
        decimal totalLpgKg = 0m;

        foreach (var line in vm.Lines)
        {
            if (!prodMap.TryGetValue(line.ProductId, out var p))
                continue;

            var unitPrice = p.DefaultUnitPrice; // Credit sale price remains controlled by Product.DefaultUnitPrice.
            var eqKg = p.EquivalentKg ?? 0m;
            var isLpg = eqKg > 0m;

            decimal qtyToStore = line.Qty;
            decimal lineTotal;
            decimal? lpgKg = null;

            if (isLpg)
            {
                if (line.EntryMode == SaleEntryMode.ByAmount)
                {
                    if (!line.Amount.HasValue || line.Amount.Value <= 0)
                        return (false, "Amount must be greater than zero.", new(), 0m, 0m);

                    if (unitPrice <= 0m)
                        return (false, "Unit price must be greater than zero.", new(), 0m, 0m);

                    lineTotal = Math.Round(line.Amount.Value, 2, MidpointRounding.AwayFromZero);
                    lpgKg = Math.Round(line.Amount.Value / unitPrice, 2, MidpointRounding.AwayFromZero);

                    // By Amount records one cylinder/line unless cashier deliberately sets a higher cylinder count.
                    if (qtyToStore <= 0) qtyToStore = 1;
                }
                else
                {
                    if (eqKg <= 0m)
                        return (false, $"Product '{p.Name}' does not have a valid EquivalentKg.", new(), 0m, 0m);

                    lpgKg = Math.Round(line.Qty * eqKg, 2, MidpointRounding.AwayFromZero);
                    lineTotal = Math.Round(line.Qty * unitPrice, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                if (line.EntryMode == SaleEntryMode.ByAmount)
                {
                    if (!line.Amount.HasValue || line.Amount.Value <= 0)
                        return (false, "Amount must be greater than zero.", new(), 0m, 0m);

                    lineTotal = Math.Round(line.Amount.Value, 2, MidpointRounding.AwayFromZero);
                    if (qtyToStore <= 0) qtyToStore = 1;
                }
                else
                {
                    lineTotal = Math.Round(line.Qty * unitPrice, 2, MidpointRounding.AwayFromZero);
                }
            }

            var item = new SaleItem
            {
                ProductId = p.Id,
                Qty = qtyToStore,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            };

            if (isLpg)
            {
                if (activeBatch is null)
                    return (false, "No ACTIVE LPG batch for your station.", new(), 0m, 0m);

                item.LpgBatchId = activeBatch.Id;
                item.LpgKg = lpgKg ?? 0m;

                if (activeBatch.CostPerKg.HasValue)
                {
                    item.UnitCostAtSale = activeBatch.CostPerKg.Value;
                    item.TotalCostAtSale = item.LpgKg.Value * activeBatch.CostPerKg.Value;
                }

                totalLpgKg += item.LpgKg.Value;
            }

            subtotal += lineTotal;
            items.Add(item);
        }

        if (items.Count == 0)
            return (false, "Add at least one valid item.", new(), 0m, 0m);

        return (true, null, items, subtotal, totalLpgKg);
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

    // Phase 2: Aging report for outstanding credit
    [HttpGet]
    public async Task<IActionResult> Aging()
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var now = DateTime.UtcNow;

        var openSales = await _db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Where(s => s.StationId == stationId && s.IsCreditSale && s.Status == SaleStatus.Completed)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var rows = new List<CreditAgingRowVm>();
        foreach (var s in openSales)
        {
            var paid = await _db.CreditPayments.AsNoTracking().Where(p => p.SaleId == s.Id).SumAsync(p => p.Amount);
            var balance = s.Total - paid;
            if (balance <= 0m) continue;

            var due = s.CreditDueDate?.Date;
            var daysPastDue = due is null ? 0 : (int)Math.Floor((now.Date - due.Value.Date).TotalDays);

            rows.Add(new CreditAgingRowVm
            {
                SaleId = s.Id,
                ReceiptNo = s.ReceiptNo,
                CustomerName = s.Customer?.Name ?? "-",
                CreatedAtUtc = s.CreatedAt,
                DueDate = due,
                DaysPastDue = daysPastDue,
                Total = s.Total,
                Paid = paid,
                Balance = balance
            });
        }

        var vm = new CreditAgingVm
        {
            AsOfUtc = now,
            Rows = rows.OrderBy(r => r.Bucket).ThenByDescending(r => r.Balance).ToList()
        };

        return View(vm);
    }

    // Phase 2: Customer statement
    [HttpGet]
    public async Task<IActionResult> Customer(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.StationId == stationId);
        if (customer is null) return NotFound();

        var now = DateTime.UtcNow;
        var lines = new List<CustomerStatementLineVm>();

        var creditSales = await _db.Sales.AsNoTracking()
            .Where(s => s.StationId == stationId && s.CustomerId == id && s.IsCreditSale && s.Status == SaleStatus.Completed)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        foreach (var s in creditSales)
        {
            lines.Add(new CustomerStatementLineVm
            {
                DateUtc = s.CreatedAt,
                Description = $"Credit Sale {s.ReceiptNo}",
                Amount = s.Total
            });

            var pays = await _db.CreditPayments.AsNoTracking()
                .Where(p => p.SaleId == s.Id)
                .OrderBy(p => p.ReceivedAtUtc)
                .ToListAsync();

            foreach (var p in pays)
            {
                lines.Add(new CustomerStatementLineVm
                {
                    DateUtc = p.ReceivedAtUtc,
                    Description = $"Payment ({p.Method}) for {s.ReceiptNo}",
                    Amount = -p.Amount
                });
            }
        }

        var vm = new CustomerStatementVm
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            AsOfUtc = now,
            Lines = lines.OrderBy(l => l.DateUtc).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StationManager")]
    public async Task<IActionResult> AddPayment(Guid id)
    {
        var stationId = await _stationCtx.GetStationIdOrThrowAsync();



        var guard = await GuardClosedDayAsync(stationId, DateTime.Today);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction("Index", "DailyClose", new { date = AsDateOnly(DateTime.Today).ToString("yyyy-MM-dd") });
        }
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


        var guard = await GuardClosedDayAsync(stationId, DateTime.Today);
        if (!guard.Allowed)
        {
            TempData["Err"] = guard.Message;
            return RedirectToAction("Index", "DailyClose", new { date = AsDateOnly(DateTime.Today).ToString("yyyy-MM-dd") });
        }
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