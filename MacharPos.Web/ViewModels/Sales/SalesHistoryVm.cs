using MacharPos.Core.Entities;

namespace MacharPos.Web.ViewModels.Sales;

public class SalesHistoryVm
{
    public List<Sale> Items { get; set; } = new();

    public string StationName { get; set; } = "";
    public string? Quick { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public decimal Total { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}