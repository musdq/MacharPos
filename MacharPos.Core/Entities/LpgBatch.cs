using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacharPos.Core.Entities
{
    public enum LpgBatchStatus { Draft = 0, Active = 1, Closed = 2 }

    public class LpgBatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StationId { get; set; }
        public Station? Station { get; set; }

        public string BatchNo { get; set; } = "";
        public string? SupplierName { get; set; }
        public DateTime DateReceived { get; set; } = DateTime.UtcNow.Date;

        public decimal QuantityKg { get; set; } // e.g., 10000 for 10MT
        public decimal SoldKg { get; set; } = 0m; // updated on each sale
        public decimal RemainingKg => QuantityKg - SoldKg;

        public decimal? CostPerKg { get; set; }
        public LpgBatchStatus Status { get; set; } = LpgBatchStatus.Draft;
    }
}
