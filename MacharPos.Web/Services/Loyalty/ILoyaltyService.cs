using MacharPos.Core.Entities;
using MacharPos.Web.ViewModels.Sales;

namespace MacharPos.Web.Services.Loyalty;

public interface ILoyaltyService
{
    Task<LoyaltySnapshot> GetSnapshotAsync(Guid stationId, Guid? customerId);
    LoyaltySaleEvaluation EvaluateSale(SaleCreateVm vm, Dictionary<Guid, Product> prodMap, Customer? customer);
    void ApplySaleLoyalty(MacharPos.Web.Data.AppDbContext db, Sale sale, Customer customer, LoyaltySaleEvaluation evaluation, string? notes = null);
    Task ReverseVoidedSaleAsync(MacharPos.Web.Data.AppDbContext db, Sale sale, string? notes = null);
}
