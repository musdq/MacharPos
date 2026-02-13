namespace MacharPos.Web.ViewModels.Credit;

public class CreditAgingVm
{
    public DateTime AsOfUtc { get; set; }

    public List<CreditAgingRowVm> Rows { get; set; } = new();

    public decimal TotalOutstanding => Rows.Sum(r => r.Balance);
}

public class CreditAgingRowVm
{
    public Guid SaleId { get; set; }
    public string ReceiptNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DueDate { get; set; }
    public int DaysPastDue { get; set; } // negative => not due yet
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }

    public string Bucket
    {
        get
        {
            if (DueDate is null) return "No Due Date";
            if (DaysPastDue <= 0) return "Not Due";
            if (DaysPastDue <= 7) return "1–7 days";
            if (DaysPastDue <= 30) return "8–30 days";
            if (DaysPastDue <= 60) return "31–60 days";
            return "60+ days";
        }
    }
}
