using System.Security.Cryptography;

namespace MacharPos.Web.Services.Expenses;

public static class ExpensePaymentReferenceGenerator
{
    public static string Generate(DateTime paidAtUtc)
    {
        var randomPart = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return $"EXP{paidAtUtc.ToUniversalTime():yyyyMMdd}{randomPart}";
    }
}
