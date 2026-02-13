namespace MacharPos.Web.ViewModels.Credit;

public class CustomerStatementVm
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "";

    public DateTime AsOfUtc { get; set; }

    public List<CustomerStatementLineVm> Lines { get; set; } = new();

    public decimal TotalDebit => Lines.Where(l => l.Amount > 0).Sum(l => l.Amount);
    public decimal TotalCredit => Lines.Where(l => l.Amount < 0).Sum(l => -l.Amount);
    public decimal Balance => TotalDebit - TotalCredit;
}

public class CustomerStatementLineVm
{
    public DateTime DateUtc { get; set; }
    public string Description { get; set; } = "";
    // Debit as positive, Credit (payment) as negative
    public decimal Amount { get; set; }
}
