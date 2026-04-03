namespace MacharPos.Web.ViewModels.Dashboard
{
    public class DashboardVm
    {
        public string StationName { get; set; } = "";

        // KPI
        public decimal LpgKgSoldThisMonth { get; set; }
        public decimal ActiveBatchRemainingKg { get; set; }

        public int LpgCylindersSoldThisMonth { get; set; }

        // Chart data (labels + values)
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlySales { get; set; } = new();

        public List<string> WeeklyLabels { get; set; } = new();
        public List<decimal> WeeklySales { get; set; } = new();

        public List<string> DailyLabels { get; set; } = new();
        public List<decimal> DailySales { get; set; } = new();

        public decimal TodaySales { get; set; }

        public decimal LpgKgSoldToday { get; set; }
        public int LpgCylindersSoldToday { get; set; }

    }
}
