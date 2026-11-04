using CafePOS.Core.BusinessRules;
using CafePOS.Core.Enums;

namespace CafePOS.Core.BusinessRules;

public static class ShiftMath
{
    public static decimal ExpectedCash(decimal openingCash, decimal cashSales, decimal cashRefunds, decimal cashExpenses) =>
        Money.Round(openingCash + cashSales - cashRefunds - cashExpenses);

    public static decimal Difference(decimal countedCash, decimal expectedCash) =>
        Money.Round(countedCash - expectedCash);
}

public static class InventoryPolicy
{
    public const string DeductionOnCompleted = "Completed";
    public const string DeductionOnServed = "Served";
    public const string DeductionOnPaid = "Paid";

    public static bool ShouldDeduct(OrderStatus status, string? configuredEvent)
    {
        var configured = string.IsNullOrWhiteSpace(configuredEvent) ? DeductionOnCompleted : configuredEvent.Trim();
        return configured.ToLowerInvariant() switch
        {
            "served" => status is OrderStatus.Served or OrderStatus.Completed,
            "paid" => status == OrderStatus.Completed,
            _ => status == OrderStatus.Completed
        };
    }

    public static bool ShouldReturnStock(OrderStatus previousStatus, string? configuredEvent) =>
        ShouldDeduct(previousStatus, configuredEvent);
}
