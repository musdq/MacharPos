namespace MacharPos.Web.Services.Loyalty;

public class LoyaltySnapshot
{
    public bool IsEligibleCustomer { get; set; }
    public bool CustomerSelected { get; set; }
    public int StampCount { get; set; }
    public int AvailableRewards { get; set; }
    public string Message { get; set; } = "";
}

public class LoyaltySaleEvaluation
{
    public bool IsEligibleCustomer { get; set; }
    public bool HasEligibleRefill { get; set; }
    public int QualifyingRefillCount { get; set; }
    public bool RewardRequested { get; set; }
    public bool RewardApplied { get; set; }
    public int RewardsRedeemed { get; set; }
    public decimal LoyaltyDiscountAmount { get; set; }
    public string Message { get; set; } = "";
}
