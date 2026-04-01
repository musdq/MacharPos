using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.ViewModels.Sales;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Services.Loyalty;

public class LoyaltyService : ILoyaltyService
{
    private const int RefillTarget = 6;
    private const decimal RewardRate = 0.25m;

    public async Task<LoyaltySnapshot> GetSnapshotAsync(Guid stationId, Guid? customerId)
    {
        if (!customerId.HasValue)
        {
            return new LoyaltySnapshot
            {
                CustomerSelected = false,
                IsEligibleCustomer = false,
                Message = "Select a named customer to use loyalty."
            };
        }

        await Task.CompletedTask;
        return new LoyaltySnapshot();
    }

    public LoyaltySaleEvaluation EvaluateSale(SaleCreateVm vm, Dictionary<Guid, Product> prodMap, Customer? customer)
    {
        var evaluation = new LoyaltySaleEvaluation();

        if (!IsEligibleCustomer(customer))
        {
            evaluation.Message = customer is null
                ? "Select a named customer to use loyalty."
                : "Walk-in or inactive customers cannot use loyalty.";
            return evaluation;
        }

        evaluation.IsEligibleCustomer = true;

        decimal bestRewardBase = 0m;
        int qualifyingRefills = 0;

        foreach (var line in vm.Lines)
        {
            if (!line.ProductId.HasValue || !prodMap.TryGetValue(line.ProductId.Value, out var product))
                continue;

            var isLpg = (product.EquivalentKg ?? 0m) > 0m;
            if (!isLpg) continue;

            if (line.EntryMode == SaleEntryMode.ByAmount)
            {
                if (line.Amount.HasValue && line.Amount.Value > 0)
                {
                    qualifyingRefills += 1;
                    bestRewardBase = Math.Max(bestRewardBase, line.Amount.Value);
                }
            }
            else
            {
                if (line.Qty > 0 && line.UnitPrice > 0)
                {
                    qualifyingRefills += (int)Math.Floor(line.Qty);
                    bestRewardBase = Math.Max(bestRewardBase, line.UnitPrice);
                }
            }
        }

        evaluation.HasEligibleRefill = qualifyingRefills > 0;
        evaluation.QualifyingRefillCount = qualifyingRefills;
        evaluation.RewardRequested = vm.ApplyLoyaltyReward;

        if (!evaluation.HasEligibleRefill)
        {
            evaluation.Message = "Loyalty applies only to LPG refill lines.";
            return evaluation;
        }

        if (vm.ApplyLoyaltyReward)
        {
            if (customer!.LoyaltyAvailableRewards <= 0)
            {
                evaluation.Message = "This customer does not have an available loyalty reward yet.";
                return evaluation;
            }

            evaluation.RewardApplied = true;
            evaluation.RewardsRedeemed = 1;
            evaluation.LoyaltyDiscountAmount = Math.Round(bestRewardBase * RewardRate, 2, MidpointRounding.AwayFromZero);
            evaluation.Message = "25% loyalty discount will be applied to one qualifying refill.";
        }
        else
        {
            evaluation.Message = "Qualifying refills will count toward the next loyalty reward.";
        }

        return evaluation;
    }

    public void ApplySaleLoyalty(AppDbContext db, Sale sale, Customer customer, LoyaltySaleEvaluation evaluation, string? notes = null)
    {
        if (!evaluation.IsEligibleCustomer || !evaluation.HasEligibleRefill)
            return;

        sale.LoyaltyRefillsEarned = evaluation.QualifyingRefillCount;

        if (evaluation.RewardApplied && evaluation.RewardsRedeemed > 0)
        {
            customer.LoyaltyAvailableRewards = Math.Max(0, customer.LoyaltyAvailableRewards - evaluation.RewardsRedeemed);
            customer.LoyaltyLifetimeRedeemed += evaluation.RewardsRedeemed;

            sale.LoyaltyRewardApplied = true;
            sale.LoyaltyRewardRedeemedCount = evaluation.RewardsRedeemed;
            sale.LoyaltyDiscountAmount = evaluation.LoyaltyDiscountAmount;

            db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
            {
                StationId = sale.StationId,
                CustomerId = customer.Id,
                SaleId = sale.Id,
                Type = LoyaltyTransactionType.Redeem,
                StampDelta = 0,
                RewardDelta = -evaluation.RewardsRedeemed,
                QualifyingRefillCount = 0,
                DiscountAmount = evaluation.LoyaltyDiscountAmount,
                Notes = notes ?? "Redeemed loyalty reward at sale checkout."
            });
        }

        if (evaluation.QualifyingRefillCount > 0)
        {
            var totalStamps = customer.LoyaltyStampCount + evaluation.QualifyingRefillCount;
            var newRewards = totalStamps / RefillTarget;
            var remainder = totalStamps % RefillTarget;

            customer.LoyaltyStampCount = remainder;
            customer.LoyaltyAvailableRewards += newRewards;
            customer.LoyaltyLifetimeEarned += newRewards;

            db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
            {
                StationId = sale.StationId,
                CustomerId = customer.Id,
                SaleId = sale.Id,
                Type = LoyaltyTransactionType.Earn,
                StampDelta = evaluation.QualifyingRefillCount,
                RewardDelta = newRewards,
                QualifyingRefillCount = evaluation.QualifyingRefillCount,
                DiscountAmount = 0m,
                Notes = notes ?? $"Earned {evaluation.QualifyingRefillCount} refill stamp(s)."
            });
        }
    }

    public async Task ReverseVoidedSaleAsync(AppDbContext db, Sale sale, string? notes = null)
    {
        if (!sale.CustomerId.HasValue)
            return;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value && c.StationId == sale.StationId);
        if (customer is null)
            return;

        var originalTxns = await db.Set<LoyaltyTransaction>()
            .Where(x => x.SaleId == sale.Id && x.CustomerId == customer.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        if (originalTxns.Count == 0)
            return;

        foreach (var txn in originalTxns)
        {
            if (txn.Type == LoyaltyTransactionType.Earn)
            {
                var currentTotalStamps = customer.LoyaltyStampCount + (customer.LoyaltyAvailableRewards * RefillTarget);
                var reversedTotalStamps = Math.Max(0, currentTotalStamps - txn.QualifyingRefillCount);

                customer.LoyaltyAvailableRewards = reversedTotalStamps / RefillTarget;
                customer.LoyaltyStampCount = reversedTotalStamps % RefillTarget;
                customer.LoyaltyLifetimeEarned = Math.Max(0, customer.LoyaltyLifetimeEarned - Math.Max(0, txn.RewardDelta));

                db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
                {
                    StationId = sale.StationId,
                    CustomerId = customer.Id,
                    SaleId = sale.Id,
                    Type = LoyaltyTransactionType.VoidReversal,
                    StampDelta = -txn.StampDelta,
                    RewardDelta = -txn.RewardDelta,
                    QualifyingRefillCount = -txn.QualifyingRefillCount,
                    DiscountAmount = 0m,
                    Notes = notes ?? "Void reversal for earned loyalty stamps."
                });
            }
            else if (txn.Type == LoyaltyTransactionType.Redeem)
            {
                customer.LoyaltyAvailableRewards += Math.Abs(txn.RewardDelta);
                customer.LoyaltyLifetimeRedeemed = Math.Max(0, customer.LoyaltyLifetimeRedeemed - Math.Abs(txn.RewardDelta));

                db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
                {
                    StationId = sale.StationId,
                    CustomerId = customer.Id,
                    SaleId = sale.Id,
                    Type = LoyaltyTransactionType.VoidReversal,
                    StampDelta = 0,
                    RewardDelta = Math.Abs(txn.RewardDelta),
                    QualifyingRefillCount = 0,
                    DiscountAmount = -txn.DiscountAmount,
                    Notes = notes ?? "Void reversal for redeemed loyalty reward."
                });
            }
        }
    }

    public static bool IsEligibleCustomer(Customer? customer)
    {
        if (customer is null) return false;
        if (!customer.IsActive || !customer.IsLoyaltyEnabled) return false;
        if (string.Equals(customer.Name?.Trim(), "Walk-in", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
