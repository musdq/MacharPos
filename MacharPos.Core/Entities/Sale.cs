using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacharPos.Core.Entities
{
    public enum PaymentMethod { Cash = 0, POS = 1, Transfer = 2, Credit = 3 }

    public enum CreditStatus
    {
        Open = 0,
        Settled = 1
    }


    public class Sale
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StationId { get; set; }
        public Station? Station { get; set; }

        public string ReceiptNo { get; set; } = "";
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public string SoldByUserId { get; set; } = ""; // Identity User Id
        public PaymentMethod PaymentMethod { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<SaleItem> Items { get; set; } = new();

        public decimal DiscountAmount { get; set; } = 0m;
        public string? DiscountApprovedByUserId { get; set; }
        public DateTime? DiscountApprovedAt { get; set; }

        public bool IsCreditSale { get; set; } = false;

        public decimal CreditAmountPaid { get; set; } = 0m;
        public decimal CreditBalance { get; set; } = 0m;

        public CreditStatus CreditStatus { get; set; } = CreditStatus.Open;

        public DateTime? CreditDueDate { get; set; } // map as 'date'
        public string? CreditNotes { get; set; }


    }

    public class SaleItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SaleId { get; set; }
        public Sale? Sale { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public decimal Qty { get; set; } // pieces
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        // For LPG refill lines only:
        public Guid? LpgBatchId { get; set; }
        public LpgBatch? LpgBatch { get; set; }
        public decimal? LpgKg { get; set; } // Qty * Product.EquivalentKg
    }
}
