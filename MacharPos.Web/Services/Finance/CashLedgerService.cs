using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.Utils;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Services.Finance;

public sealed record CashDaySnapshot(
    decimal OpeningMainCash,
    decimal OpeningPettyCash,
    decimal CashSales,
    decimal CashCreditCollections,
    decimal PaidMainCashExpenses,
    decimal PaidPettyCashExpenses,
    decimal ManualMainCashNet,
    decimal ManualPettyCashNet,
    decimal ExpectedMainCash,
    decimal ExpectedPettyCash);

public static class CashReconciliationCalculator
{
    public static decimal Expected(decimal opening, decimal salesAndCollections, decimal manualNet, decimal paidExpenses)
        => opening + salesAndCollections + manualNet - paidExpenses;
}

public interface ICashLedgerService
{
    Task<CashDaySnapshot> CalculateDayAsync(Guid stationId, DateTime localDate);
}

public sealed class CashLedgerService : ICashLedgerService
{
    private readonly AppDbContext _db;

    public CashLedgerService(AppDbContext db) => _db = db;

    public async Task<CashDaySnapshot> CalculateDayAsync(Guid stationId, DateTime localDate)
    {
        localDate = BusinessTime.AsDateOnlyLocal(localDate);
        var (utcStart, utcEnd) = BusinessTime.UtcRangeForLocalDate(localDate);

        var previous = await _db.CashReconciliations.AsNoTracking()
            .Where(x => x.StationId == stationId && x.IsClosed && x.ReconciliationDate < localDate)
            .OrderByDescending(x => x.ReconciliationDate)
            .Select(x => new { x.ActualMainCash, x.ActualPettyCash })
            .FirstOrDefaultAsync();

        var cashSaleAmounts = await _db.Sales.AsNoTracking()
            .Where(x => x.StationId == stationId && x.Status == SaleStatus.Completed &&
                        !x.IsCreditSale && x.PaymentMethod == PaymentMethod.Cash &&
                        x.CreatedAt >= utcStart && x.CreatedAt < utcEnd)
            .Select(x => x.Total).ToListAsync();
        var cashSales = cashSaleAmounts.Sum();

        var creditCollectionAmounts = await _db.CreditPayments.AsNoTracking()
            .Where(x => x.Sale!.StationId == stationId && x.Sale.Status == SaleStatus.Completed &&
                        x.Method == PaymentMethod.Cash &&
                        x.ReceivedAtUtc >= utcStart && x.ReceivedAtUtc < utcEnd)
            .Select(x => x.Amount).ToListAsync();
        var creditCollections = creditCollectionAmounts.Sum();

        var mainExpenseAmounts = await _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.StationId == stationId && x.Status == ExpenseStatus.Approved &&
                        x.PaymentStatus == ExpensePaymentStatus.Paid &&
                        x.PaymentMethod == ExpensePaymentMethod.Cash &&
                        x.PaidAtUtc >= utcStart && x.PaidAtUtc < utcEnd)
            .Select(x => x.Amount).ToListAsync();
        var mainExpenses = mainExpenseAmounts.Sum();

        var pettyExpenseAmounts = await _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.StationId == stationId && x.Status == ExpenseStatus.Approved &&
                        x.PaymentStatus == ExpensePaymentStatus.Paid &&
                        x.PaymentMethod == ExpensePaymentMethod.PettyCash &&
                        x.PaidAtUtc >= utcStart && x.PaidAtUtc < utcEnd)
            .Select(x => x.Amount).ToListAsync();
        var pettyExpenses = pettyExpenseAmounts.Sum();

        var movements = await _db.CashMovements.AsNoTracking()
            .Where(x => x.StationId == stationId &&
                        x.OccurredAtUtc >= utcStart && x.OccurredAtUtc < utcEnd)
            .Select(x => new { x.MainCashImpact, x.PettyCashImpact })
            .ToListAsync();

        var openingMain = previous?.ActualMainCash ?? 0m;
        var openingPetty = previous?.ActualPettyCash ?? 0m;
        var manualMain = movements.Sum(x => x.MainCashImpact);
        var manualPetty = movements.Sum(x => x.PettyCashImpact);
        var expectedMain = CashReconciliationCalculator.Expected(
            openingMain, cashSales + creditCollections, manualMain, mainExpenses);
        var expectedPetty = CashReconciliationCalculator.Expected(
            openingPetty, 0m, manualPetty, pettyExpenses);

        return new CashDaySnapshot(
            openingMain, openingPetty, cashSales, creditCollections,
            mainExpenses, pettyExpenses, manualMain, manualPetty,
            expectedMain, expectedPetty);
    }
}
