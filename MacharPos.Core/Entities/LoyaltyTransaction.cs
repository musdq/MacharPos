using System;

namespace MacharPos.Core.Entities
{
    public enum LoyaltyTransactionType
    {
        Earn = 1,
        Redeem = 2,
        VoidReversal = 3,
        ManualAdjustment = 4
    }

    public class LoyaltyTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid StationId { get; set; }
        public Station? Station { get; set; }

        public Guid CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public Guid? SaleId { get; set; }
        public Sale? Sale { get; set; }

        public LoyaltyTransactionType Type { get; set; }
        public int StampDelta { get; set; }
        public int RewardDelta { get; set; }
        public int QualifyingRefillCount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
