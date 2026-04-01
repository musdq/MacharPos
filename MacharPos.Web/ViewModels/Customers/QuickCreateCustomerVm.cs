namespace MacharPos.Web.ViewModels.Customers
{
    public class QuickCreateCustomerVm
    {
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public bool IsLoyaltyEnabled { get; set; } = true;
    }
}
