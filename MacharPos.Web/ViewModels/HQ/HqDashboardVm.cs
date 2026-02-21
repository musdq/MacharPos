namespace MacharPos.Web.ViewModels.HQ;

public class HqDashboardVm
{
    public DateTime ReportDate { get; set; } // Nigeria local date (date-only)

    // Headline totals (all stations)
    public decimal TotalSalesAmount { get; set; }

    // Phase 6.2 (Margin Engine)
    public decimal TotalCostAmount { get; set; }
    public decimal GrossProfitAmount { get; set; }
    public decimal GrossMarginPercent { get; set; }

    public decimal CashAmount { get; set; }
    public decimal PosAmount { get; set; }
    public decimal TransferAmount { get; set; }
    public decimal CreditAmount { get; set; }

    // Volume KPIs (best-effort depending on your product catalog)
    public decimal LpgKgSold { get; set; }
    public decimal PmsLitresSold { get; set; }
    public decimal AgoLitresSold { get; set; }

    public List<StationKpiRow> Stations { get; set; } = new();

    public List<string> Alerts { get; set; } = new();
}

public class StationKpiRow
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = "";

    public decimal SalesAmount { get; set; }

    // Phase 6.2 (Margin Engine)
    public decimal CostAmount { get; set; }
    public decimal GrossProfitAmount { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal CashAmount { get; set; }
    public decimal PosAmount { get; set; }
    public decimal TransferAmount { get; set; }
    public decimal CreditAmount { get; set; }

    public decimal LpgKgSold { get; set; }
    public decimal PmsLitresSold { get; set; }
    public decimal AgoLitresSold { get; set; }
}
