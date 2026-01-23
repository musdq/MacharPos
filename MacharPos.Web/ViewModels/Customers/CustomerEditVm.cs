using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Customers;

public class CustomerEditVm
{
    public Guid? Id { get; set; }

    [Required, MinLength(2)]
    public string Name { get; set; } = "";

    public string? Phone { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}