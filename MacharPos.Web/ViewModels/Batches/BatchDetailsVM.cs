using MacharPos.Core.Entities;

namespace MacharPos.Web.ViewModels.Batches
{
    public class BatchDetailsVM
    {
        public LpgBatch Batch { get; set; }
        public List<SaleItem> Sales { get; set; }

        public decimal TotalKgSold { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit { get; set; }

        public int TotalCylindersSold { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    }
}
