using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.HQ;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Services.HQ;

public class HqDashboardService : IHqDashboardService
{
    private readonly AppDbContext _db;

    // Tune this threshold as you like (kg)
    private const decimal LOW_LPG_REMAINING_KG_THRESHOLD = 1000m; // 1MT

    public HqDashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HqDashboardVm> BuildAsync(DateTime reportDateLocal)
    {
        // Convert business date (Nigeria local) -> UTC [start, end)
        var (utcStart, utcEndExclusive) = BusinessTime.UtcRangeForLocalDate(reportDateLocal);

        // Stations (for names)
        var stations = await _db.Stations
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        // Sales for day (exclude voids)
        var salesQ = _db.Sales.AsNoTracking()
            .Where(s =>
                s.Status == SaleStatus.Completed &&
                s.CreatedAt >= utcStart &&
                s.CreatedAt < utcEndExclusive);

        // Headline totals
        var totals = await salesQ
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(x => x.Total),
                Cash = g.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Total),
                Pos = g.Where(x => x.PaymentMethod == PaymentMethod.POS).Sum(x => x.Total),
                Transfer = g.Where(x => x.PaymentMethod == PaymentMethod.Transfer).Sum(x => x.Total),
                Credit = g.Where(x => x.PaymentMethod == PaymentMethod.Credit).Sum(x => x.Total),
            })
            .FirstOrDefaultAsync()
            ?? new { Total = 0m, Cash = 0m, Pos = 0m, Transfer = 0m, Credit = 0m };

        // Station breakdown totals
        var stationTotals = await salesQ
            .GroupBy(s => s.StationId)
            .Select(g => new
            {
                StationId = g.Key,
                Total = g.Sum(x => x.Total),
                Cash = g.Where(x => x.PaymentMethod == PaymentMethod.Cash).Sum(x => x.Total),
                Pos = g.Where(x => x.PaymentMethod == PaymentMethod.POS).Sum(x => x.Total),
                Transfer = g.Where(x => x.PaymentMethod == PaymentMethod.Transfer).Sum(x => x.Total),
                Credit = g.Where(x => x.PaymentMethod == PaymentMethod.Credit).Sum(x => x.Total),
            })
            .ToListAsync();


        // Phase 6.2: Cost + Gross Profit (uses SaleItem.TotalCostAtSale, stamped at time of sale)
        var costByStation = await _db.SaleItems.AsNoTracking()
            .Join(
                salesQ,
                i => i.SaleId,
                s => s.Id,
                (i, s) => new { s.StationId, Cost = (i.TotalCostAtSale ?? 0m) }
            )
            .GroupBy(x => x.StationId)
            .Select(g => new { StationId = g.Key, Cost = g.Sum(x => x.Cost) })
            .ToListAsync();

        var totalCost = costByStation.Sum(x => x.Cost);

        // LPG kg sold (based on SaleItem.LpgKg)
        // We join against Sales to re-use the day filter safely.
        var lpgByStation = await _db.SaleItems.AsNoTracking()
            .Where(i => i.LpgKg != null)
            .Join(
                salesQ,
                i => i.SaleId,
                s => s.Id,
                (i, s) => new { s.StationId, Kg = i.LpgKg ?? 0m }
            )
            .GroupBy(x => x.StationId)
            .Select(g => new { StationId = g.Key, Kg = g.Sum(x => x.Kg) })
            .ToListAsync();

        var totalLpgKg = lpgByStation.Sum(x => x.Kg);

        // PMS/AGO litres sold (best effort)
        // If you have PMS/AGO products as "litres", we treat SaleItem.Qty as litres.
        // Matching rule: category or product name contains PMS/AGO (case-insensitive).
        var fuelByStation = await _db.SaleItems.AsNoTracking()
            .Join(
                salesQ,
                i => i.SaleId,
                s => s.Id,
                (i, s) => new { i, s.StationId }
            )
            .Join(
                _db.Products.AsNoTracking(),
                x => x.i.ProductId,
                p => p.Id,
                (x, p) => new { x.StationId, x.i.Qty, ProductName = p.Name, CategoryId = p.CategoryId }
            )
            .Join(
                _db.ProductCategories.AsNoTracking(),
                x => x.CategoryId,
                c => c.Id,
                (x, c) => new { x.StationId, x.Qty, x.ProductName, CategoryName = c.Name }
            )
            .Select(x => new
            {
                x.StationId,
                Qty = x.Qty,
                Key = (x.CategoryName + " " + x.ProductName).ToUpper()
            })
            .ToListAsync();

        decimal totalPms = 0m, totalAgo = 0m;
        var pmsByStation = new Dictionary<Guid, decimal>();
        var agoByStation = new Dictionary<Guid, decimal>();

        foreach (var row in fuelByStation)
        {
            if (row.Key.Contains("PMS"))
            {
                totalPms += row.Qty;
                pmsByStation[row.StationId] = pmsByStation.GetValueOrDefault(row.StationId) + row.Qty;
            }
            if (row.Key.Contains("AGO") || row.Key.Contains("DIESEL"))
            {
                totalAgo += row.Qty;
                agoByStation[row.StationId] = agoByStation.GetValueOrDefault(row.StationId) + row.Qty;
            }
        }

        // Alerts: LPG low remaining on ACTIVE batch
        var batchByStation = await _db.LpgBatches.AsNoTracking()
            .Where(b => b.Status == LpgBatchStatus.Active)
            .Select(b => new { b.StationId, b.BatchNo, b.QuantityKg, b.SoldKg })
            .ToListAsync();

        var alerts = new List<string>();
        foreach (var b in batchByStation)
        {
            var remaining = b.QuantityKg - b.SoldKg;
            if (remaining <= LOW_LPG_REMAINING_KG_THRESHOLD)
            {
                var stationName = stations.FirstOrDefault(s => s.Id == b.StationId)?.Name ?? b.StationId.ToString();
                alerts.Add($"LPG batch {b.BatchNo} low at {stationName}: {remaining:N0} kg remaining");
            }
        }

        // Assemble station rows
        var stationRows = new List<StationKpiRow>();
        foreach (var st in stations)
        {
            var stTotals = stationTotals.FirstOrDefault(x => x.StationId == st.Id);
            var stLpg = lpgByStation.FirstOrDefault(x => x.StationId == st.Id);
            var stCost = costByStation.FirstOrDefault(x => x.StationId == st.Id);


            stationRows.Add(new StationKpiRow
            {
                StationId = st.Id,
                StationName = st.Name,

                SalesAmount = stTotals?.Total ?? 0m,
                CashAmount = stTotals?.Cash ?? 0m,
                PosAmount = stTotals?.Pos ?? 0m,
                TransferAmount = stTotals?.Transfer ?? 0m,
                CreditAmount = stTotals?.Credit ?? 0m,

                CostAmount = stCost?.Cost ?? 0m,
                GrossProfitAmount = (stTotals?.Total ?? 0m) - (stCost?.Cost ?? 0m),
                GrossMarginPercent = ((stTotals?.Total ?? 0m) > 0m) ? (((stTotals?.Total ?? 0m) - (stCost?.Cost ?? 0m)) / (stTotals?.Total ?? 0m) * 100m) : 0m,

                LpgKgSold = stLpg?.Kg ?? 0m,
                PmsLitresSold = pmsByStation.GetValueOrDefault(st.Id),
                AgoLitresSold = agoByStation.GetValueOrDefault(st.Id),
            });
        }

        return new HqDashboardVm
        {
            ReportDate = reportDateLocal.Date,
            TotalSalesCount = await salesQ.CountAsync(),
            TotalSalesAmount = totals.Total,
            CashAmount = totals.Cash,
            PosAmount = totals.Pos,
            TransferAmount = totals.Transfer,
            CreditAmount = totals.Credit,

            TotalCostAmount = totalCost,
            GrossProfitAmount = totals.Total - totalCost,
            GrossMarginPercent = (totals.Total > 0m) ? ((totals.Total - totalCost) / totals.Total * 100m) : 0m,

            LpgKgSold = totalLpgKg,
            PmsLitresSold = totalPms,
            AgoLitresSold = totalAgo,
            Stations = stationRows,
            Alerts = alerts
        };
    }
}
