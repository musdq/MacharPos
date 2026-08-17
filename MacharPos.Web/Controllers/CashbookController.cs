using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Services.Expenses;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.Expenses;
using MacharPos.Web.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize(Roles = "Admin,StationManager")]
public class CashbookController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationContext;
    private readonly UserManager<ApplicationUser> _users;

    public CashbookController(AppDbContext db, IStationContext stationContext, UserManager<ApplicationUser> users)
    {
        _db = db;
        _stationContext = stationContext;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CashbookFilterVm filter)
    {
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
            filter.StationId = await _stationContext.GetStationIdOrThrowAsync();

        var range = ExpenseReporting.ResolveRange(filter.Period, filter.StartDate, filter.EndDate);
        filter.Period = range.Period;
        filter.StartDate = range.StartLocal;
        filter.EndDate = range.EndLocal;

        var entries = new List<CashbookRawEntry>();

        var salesQuery = _db.Sales.AsNoTracking()
            .Where(x => x.Status == SaleStatus.Completed && !x.IsCreditSale &&
                        x.PaymentMethod == PaymentMethod.Cash &&
                        x.CreatedAt >= range.UtcStart && x.CreatedAt < range.UtcEndExclusive);
        if (filter.StationId.HasValue)
            salesQuery = salesQuery.Where(x => x.StationId == filter.StationId.Value);
        entries.AddRange(await salesQuery.Select(x => new CashbookRawEntry
        {
            OccurredAtUtc = x.CreatedAt,
            StationName = x.Station != null ? x.Station.Name : "",
            Source = "Cash sale",
            Description = "Receipt " + x.ReceiptNo,
            Reference = x.ReceiptNo,
            UserId = x.SoldByUserId,
            MainCashImpact = x.Total
        }).ToListAsync());

        var collectionsQuery = _db.CreditPayments.AsNoTracking()
            .Where(x => x.Sale!.Status == SaleStatus.Completed && x.Method == PaymentMethod.Cash &&
                        x.ReceivedAtUtc >= range.UtcStart && x.ReceivedAtUtc < range.UtcEndExclusive);
        if (filter.StationId.HasValue)
            collectionsQuery = collectionsQuery.Where(x => x.Sale!.StationId == filter.StationId.Value);
        entries.AddRange(await collectionsQuery.Select(x => new CashbookRawEntry
        {
            OccurredAtUtc = x.ReceivedAtUtc,
            StationName = x.Sale!.Station != null ? x.Sale.Station.Name : "",
            Source = "Credit collection",
            Description = "Credit payment for " + x.Sale.ReceiptNo,
            Reference = x.Sale.ReceiptNo,
            UserId = x.ReceivedByUserId,
            MainCashImpact = x.Amount
        }).ToListAsync());

        var expensesQuery = _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.Status == ExpenseStatus.Approved &&
                        x.PaymentStatus == ExpensePaymentStatus.Paid &&
                        (x.PaymentMethod == ExpensePaymentMethod.Cash ||
                         x.PaymentMethod == ExpensePaymentMethod.PettyCash) &&
                        x.PaidAtUtc >= range.UtcStart && x.PaidAtUtc < range.UtcEndExclusive);
        if (filter.StationId.HasValue)
            expensesQuery = expensesQuery.Where(x => x.StationId == filter.StationId.Value);
        entries.AddRange(await expensesQuery.Select(x => new CashbookRawEntry
        {
            OccurredAtUtc = x.PaidAtUtc!.Value,
            StationName = x.Station != null ? x.Station.Name : "",
            Source = x.PaymentMethod == ExpensePaymentMethod.PettyCash ? "Petty-cash expense" : "Cash expense",
            Description = x.Reason,
            Reference = x.PaymentReference,
            UserId = x.PaidByUserId,
            MainCashImpact = x.PaymentMethod == ExpensePaymentMethod.Cash ? -x.Amount : 0m,
            PettyCashImpact = x.PaymentMethod == ExpensePaymentMethod.PettyCash ? -x.Amount : 0m
        }).ToListAsync());

        var manualQuery = _db.CashMovements.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= range.UtcStart && x.OccurredAtUtc < range.UtcEndExclusive);
        if (filter.StationId.HasValue)
            manualQuery = manualQuery.Where(x => x.StationId == filter.StationId.Value);
        var manual = await manualQuery.Select(x => new CashbookRawEntry
        {
            ManualMovementId = x.Id,
            OccurredAtUtc = x.OccurredAtUtc,
            StationName = x.Station != null ? x.Station.Name : "",
            Source = x.Type.ToString(),
            Description = x.Description,
            Reference = x.Reference,
            UserId = x.EnteredByUserId,
            MainCashImpact = x.MainCashImpact,
            PettyCashImpact = x.PettyCashImpact,
            IsReversal = x.Type == CashMovementType.Reversal
        }).ToListAsync();
        entries.AddRange(manual);

        var reversedIdList = await _db.CashMovements.AsNoTracking()
            .Where(x => x.ReversesMovementId != null)
            .Select(x => x.ReversesMovementId!.Value)
            .ToListAsync();
        var reversedIds = reversedIdList.ToHashSet();
        var userIds = entries.Where(x => !string.IsNullOrWhiteSpace(x.UserId)).Select(x => x.UserId!).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id);

        var rows = entries.OrderByDescending(x => x.OccurredAtUtc).Select(x => new CashbookEntryVm
        {
            ManualMovementId = x.ManualMovementId,
            OccurredAtLocal = BusinessTime.ToLocal(x.OccurredAtUtc),
            StationName = x.StationName,
            Source = FinanceDisplay.MovementSource(x.Source),
            Description = x.Description,
            Reference = x.Reference,
            EnteredBy = x.UserId is null ? "System" : userNames.GetValueOrDefault(x.UserId, x.UserId),
            MainCashImpact = x.MainCashImpact,
            PettyCashImpact = x.PettyCashImpact,
            CanReverse = x.ManualMovementId.HasValue && !x.IsReversal && !reversedIds.Contains(x.ManualMovementId.Value)
        }).ToList();

        var vm = new CashbookVm
        {
            Filter = filter,
            NewMovement = new CashMovementCreateVm
            {
                StationId = filter.StationId,
                OccurredAtLocal = BusinessTime.LocalNow()
            },
            Stations = await LoadStationsAsync(),
            Entries = rows.Take(1000).ToList(),
            RangeStartLocal = range.StartLocal,
            RangeEndLocal = range.EndLocal,
            PeriodLabel = range.Label,
            IsAdmin = isAdmin,
            MainCashIn = rows.Where(x => x.MainCashImpact > 0).Sum(x => x.MainCashImpact),
            MainCashOut = -rows.Where(x => x.MainCashImpact < 0).Sum(x => x.MainCashImpact),
            PettyCashIn = rows.Where(x => x.PettyCashImpact > 0).Sum(x => x.PettyCashImpact),
            PettyCashOut = -rows.Where(x => x.PettyCashImpact < 0).Sum(x => x.PettyCashImpact)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMovement(CashMovementCreateVm form)
    {
        var isAdmin = User.IsInRole("Admin");
        var stationId = isAdmin
            ? form.StationId
            : await _stationContext.GetStationIdOrThrowAsync();

        if (!Enum.IsDefined(form.Type))
            ModelState.AddModelError(nameof(form.Type), "Choose a valid movement type.");
        if (!Enum.IsDefined(form.Account))
            ModelState.AddModelError(nameof(form.Account), "Choose a valid cash account.");

        if (!stationId.HasValue || !await _db.Stations.AnyAsync(x => x.Id == stationId && x.IsActive))
            ModelState.AddModelError(nameof(form.StationId), "Choose an active station.");
        if (form.Type is CashMovementType.StationTransferIn or CashMovementType.Reversal)
            ModelState.AddModelError(nameof(form.Type), "That movement type is created automatically.");
        if (form.Type == CashMovementType.StationTransferOut && !isAdmin)
            ModelState.AddModelError(nameof(form.Type), "Only an admin can transfer cash between stations.");
        if (form.Type == CashMovementType.StationTransferOut &&
            (!form.DestinationStationId.HasValue || form.DestinationStationId == stationId ||
             !await _db.Stations.AnyAsync(x => x.Id == form.DestinationStationId && x.IsActive)))
            ModelState.AddModelError(nameof(form.DestinationStationId), "Choose a different active destination station.");

        if (!ModelState.IsValid)
        {
            TempData["Err"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Index), new { stationId = form.StationId });
        }

        var userId = _users.GetUserId(User) ?? throw new InvalidOperationException("The signed-in user could not be identified.");
        var occurredAtUtc = BusinessTime.ToUtc(form.OccurredAtLocal);
        var (mainImpact, pettyImpact) = FinanceDisplay.Impacts(form.Type, form.Account, form.Amount);
        var transferGroupId = form.Type == CashMovementType.StationTransferOut ? Guid.NewGuid() : (Guid?)null;

        _db.CashMovements.Add(new CashMovement
        {
            StationId = stationId!.Value,
            OccurredAtUtc = occurredAtUtc,
            Type = form.Type,
            Amount = form.Amount,
            MainCashImpact = mainImpact,
            PettyCashImpact = pettyImpact,
            Description = form.Description.Trim(),
            Reference = string.IsNullOrWhiteSpace(form.Reference) ? null : form.Reference.Trim(),
            EnteredByUserId = userId,
            CounterpartyStationId = form.DestinationStationId,
            TransferGroupId = transferGroupId
        });

        if (form.Type == CashMovementType.StationTransferOut)
        {
            _db.CashMovements.Add(new CashMovement
            {
                StationId = form.DestinationStationId!.Value,
                OccurredAtUtc = occurredAtUtc,
                Type = CashMovementType.StationTransferIn,
                Amount = form.Amount,
                MainCashImpact = form.Amount,
                Description = form.Description.Trim(),
                Reference = string.IsNullOrWhiteSpace(form.Reference) ? null : form.Reference.Trim(),
                EnteredByUserId = userId,
                CounterpartyStationId = stationId,
                TransferGroupId = transferGroupId
            });
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Cash movement recorded.";
        return RedirectToAction(nameof(Index), new { stationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reverse(Guid id, string reason, Guid? stationId)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length < 3 || reason.Length > 500)
        {
            TempData["Err"] = "A reversal reason between 3 and 500 characters is required.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        var target = await _db.CashMovements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (target is null || target.Type == CashMovementType.Reversal)
        {
            TempData["Err"] = "That movement cannot be reversed.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        var group = target.TransferGroupId.HasValue
            ? await _db.CashMovements.AsNoTracking().Where(x => x.TransferGroupId == target.TransferGroupId && x.Type != CashMovementType.Reversal).ToListAsync()
            : new List<CashMovement> { target };

        if (!User.IsInRole("Admin"))
        {
            var homeStationId = await _stationContext.GetStationIdOrThrowAsync();
            if (group.Any(x => x.StationId != homeStationId))
                return Forbid();
        }

        var groupIds = group.Select(x => x.Id).ToList();
        if (await _db.CashMovements.AnyAsync(x => x.ReversesMovementId != null && groupIds.Contains(x.ReversesMovementId.Value)))
        {
            TempData["Err"] = "This movement has already been reversed.";
            return RedirectToAction(nameof(Index), new { stationId });
        }

        var userId = _users.GetUserId(User) ?? throw new InvalidOperationException("The signed-in user could not be identified.");
        var reversalGroupId = group.Count > 1 ? Guid.NewGuid() : (Guid?)null;
        foreach (var item in group)
        {
            _db.CashMovements.Add(new CashMovement
            {
                StationId = item.StationId,
                OccurredAtUtc = DateTime.UtcNow,
                Type = CashMovementType.Reversal,
                Amount = item.Amount,
                MainCashImpact = -item.MainCashImpact,
                PettyCashImpact = -item.PettyCashImpact,
                Description = "Reversal: " + reason,
                Reference = item.Reference,
                EnteredByUserId = userId,
                CounterpartyStationId = item.CounterpartyStationId,
                TransferGroupId = reversalGroupId,
                ReversesMovementId = item.Id
            });
        }

        await _db.SaveChangesAsync();
        TempData["Ok"] = "The cash movement was reversed; the original audit record was preserved.";
        return RedirectToAction(nameof(Index), new { stationId });
    }

    private Task<List<ExpenseStationOption>> LoadStationsAsync()
        => _db.Stations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new ExpenseStationOption(x.Id, x.Name)).ToListAsync();

    private sealed class CashbookRawEntry
    {
        public Guid? ManualMovementId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string StationName { get; set; } = "";
        public string Source { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Reference { get; set; }
        public string? UserId { get; set; }
        public decimal MainCashImpact { get; set; }
        public decimal PettyCashImpact { get; set; }
        public bool IsReversal { get; set; }
    }
}

public static class FinanceDisplay
{
    public static string MovementSource(string source) => source switch
    {
        nameof(CashMovementType.BankDeposit) => "Bank deposit",
        nameof(CashMovementType.PettyCashTopUp) => "Petty-cash top-up",
        nameof(CashMovementType.StationTransferOut) => "Station transfer out",
        nameof(CashMovementType.StationTransferIn) => "Station transfer in",
        nameof(CashMovementType.OtherCashIn) => "Other cash in",
        nameof(CashMovementType.OtherCashOut) => "Other cash out",
        nameof(CashMovementType.Reversal) => "Reversal",
        _ => source
    };

    public static (decimal Main, decimal Petty) Impacts(CashMovementType type, CashAccount account, decimal amount)
        => type switch
        {
            CashMovementType.BankDeposit => (-amount, 0m),
            CashMovementType.PettyCashTopUp => (-amount, amount),
            CashMovementType.StationTransferOut => (-amount, 0m),
            CashMovementType.OtherCashIn when account == CashAccount.MainCash => (amount, 0m),
            CashMovementType.OtherCashIn => (0m, amount),
            CashMovementType.OtherCashOut when account == CashAccount.MainCash => (-amount, 0m),
            CashMovementType.OtherCashOut => (0m, -amount),
            _ => throw new InvalidOperationException("Unsupported manual cash movement type.")
        };
}
