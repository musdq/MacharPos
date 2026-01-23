namespace MacharPos.Web.ViewModels.Sales;

public class SalesFilterVm
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Quick { get; set; } // today, week, month
}