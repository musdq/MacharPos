using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Customers;

public class CustomerEditVm
{
    public Guid? Id { get; set; }

    [Required, MinLength(2)]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? Address { get; set; }

    // Phase 2: credit controls
    public bool IsCreditApproved { get; set; } = false;

    [Range(0, 1000000000)]
    public decimal CreditLimit { get; set; } = 0m;

    [Range(0, 365)]
    public int CreditDueDays { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}