using DocumentFormat.OpenXml.InkML;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Services.HQ;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStationContext _stationCtx;
        private readonly IHqDashboardService _hq;

        public DashboardController(AppDbContext db, IStationContext stationCtx, IHqDashboardService hq)
        {
            _db = db;
            _stationCtx = stationCtx;
            _hq = hq;
        }


        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var stationId = await _stationCtx.GetStationIdOrThrowAsync();
            var station = await _db.Stations.AsNoTracking()
                .FirstAsync(s => s.Id == stationId);

            var nowUtc = DateTime.UtcNow;

            // Today range (UTC)
            var todayStart = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);
            var todayEnd = todayStart.AddDays(1);

            // Month range (UTC)
            var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            // Week range (Mon..Sun) in UTC
            DateTime StartOfWeekUtc(DateTime d)
            {
                var date = d.Date;
                var diff = ((int)date.DayOfWeek + 6) % 7; // Monday=0
                return DateTime.SpecifyKind(date.AddDays(-diff), DateTimeKind.Utc);
            }

            var weekStart = StartOfWeekUtc(nowUtc);
            var weekEnd = weekStart.AddDays(7);

            // Pull sales for month
            var sales = await _db.Sales.AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.StationId == stationId &&
                            s.CreatedAt >= monthStart &&
                            s.CreatedAt < monthEnd)
                .ToListAsync();

            // KPI: Today's sales
            var todaySales = sales
                .Where(s => s.CreatedAt >= todayStart && s.CreatedAt < todayEnd)
                .Sum(s => s.Total);

            // KPI: LPG kg sold today
            var lpgKgSoldToday = sales
                .Where(s => s.CreatedAt >= todayStart && s.CreatedAt < todayEnd)
                .SelectMany(s => s.Items)
                .Sum(i => i.LpgKg ?? 0m);

            // KPI: cylinders sold today
            var cylindersSoldToday = sales
                .Where(s => s.CreatedAt >= todayStart && s.CreatedAt < todayEnd)
                .SelectMany(s => s.Items)
                .Where(i => (i.LpgKg ?? 0m) > 0m)
                .Sum(i => i.Qty);

            // KPI: LPG kg sold this month
            var lpgKgSoldThisMonth = sales
                .SelectMany(s => s.Items)
                .Sum(i => i.LpgKg ?? 0m);

            // KPI: cylinders sold this month
            // Assumption: each cylinder transaction = Qty 1, including "by amount"
            var cylindersSoldThisMonth = sales
                .SelectMany(s => s.Items)
                .Where(i => (i.LpgKg ?? 0m) > 0m)
                .Sum(i => i.Qty);

            // KPI: active batch remaining kg
            var activeBatch = await _db.LpgBatches.AsNoTracking()
                .FirstOrDefaultAsync(b =>
                    b.StationId == stationId &&
                    b.Status == MacharPos.Core.Entities.LpgBatchStatus.Active);

            var remainingKg = activeBatch?.RemainingKg ?? 0m;

            // MONTHLY BAR (days of current month)
            var daysInMonth = DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month);
            var monthDays = Enumerable.Range(1, daysInMonth)
                .Select(d => new DateTime(nowUtc.Year, nowUtc.Month, d, 0, 0, 0, DateTimeKind.Utc))
                .ToList();

            var monthlyLabels = monthDays.Select(d => d.Day.ToString()).ToList();
            var monthlySales = monthDays.Select(d =>
            {
                var dEnd = d.AddDays(1);
                return sales
                    .Where(s => s.CreatedAt >= d && s.CreatedAt < dEnd)
                    .Sum(s => s.Total);
            }).ToList();

            // WEEKLY BAR (Mon..Sun)
            var weekSalesData = await _db.Sales.AsNoTracking()
                .Where(s => s.StationId == stationId &&
                            s.CreatedAt >= weekStart &&
                            s.CreatedAt < weekEnd)
                .ToListAsync();

            var weekDays = Enumerable.Range(0, 7)
                .Select(i => weekStart.AddDays(i))
                .ToList();

            var weeklyLabels = weekDays
                .Select(d => d.ToString("ddd"))
                .ToList();

            var weeklySales = weekDays.Select(d =>
            {
                var dEnd = d.AddDays(1);
                return weekSalesData
                    .Where(s => s.CreatedAt >= d && s.CreatedAt < dEnd)
                    .Sum(s => s.Total);
            }).ToList();

            var vm = new DashboardVm
            {
                StationName = station.Name,
                TodaySales = todaySales,
                LpgKgSoldToday = lpgKgSoldToday,
                LpgCylindersSoldToday = (int)cylindersSoldToday,
                LpgKgSoldThisMonth = lpgKgSoldThisMonth,
                ActiveBatchRemainingKg = remainingKg,
                LpgCylindersSoldThisMonth = (int)cylindersSoldThisMonth,
                MonthlyLabels = monthlyLabels,
                MonthlySales = monthlySales,
                WeeklyLabels = weeklyLabels,
                WeeklySales = weeklySales
            };

            return View(vm);
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> HQIndex([FromQuery] DateTime? date = null)
        {
            // Phase 6.1 uses *business day* (Nigeria local date), consistent with Daily Close.
            var reportDate = (date?.Date ?? BusinessTime.TodayLocalDate());

            var vm = await _hq.BuildAsync(reportDate);
            return View(vm);
        }

    }
}
