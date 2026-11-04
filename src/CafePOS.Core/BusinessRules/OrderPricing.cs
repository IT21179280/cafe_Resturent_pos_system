using CafePOS.Core.Enums;

namespace CafePOS.Core.BusinessRules;

public record PricingLine(decimal UnitPrice, decimal Quantity, decimal LineDiscount, decimal TaxRate, decimal ModifierTotal = 0);

public record LineCalculation(decimal Net, decimal DiscountShare, decimal TaxableBase, decimal Tax, decimal Total);

public record OrderTotals(decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal ServiceCharge, decimal Total,
    IReadOnlyList<LineCalculation> Lines);

public static class OrderPricing
{
    /// <summary>
    /// Server-side authority for all order money. Discounts are applied proportionally to lines
    /// before tax so item-level tax rates remain correct.
    /// </summary>
    public static OrderTotals Calculate(IReadOnlyList<PricingLine> lines, DiscountType discountType, decimal discountValue,
        decimal serviceChargeRatePercent)
    {
        if (lines is null || lines.Count == 0)
            return new OrderTotals(0, 0, 0, 0, 0, Array.Empty<LineCalculation>());

        var nets = new decimal[lines.Count];
        decimal subtotal = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var gross = line.UnitPrice * line.Quantity + line.ModifierTotal;
            var net = Money.Round(gross - line.LineDiscount);
            if (net < 0) net = 0;
            nets[i] = net;
            subtotal += net;
        }
        subtotal = Money.Round(subtotal);

        decimal discount = 0;
        if (discountType == DiscountType.Percentage && discountValue > 0)
            discount = Money.Round(subtotal * Money.Clamp(discountValue, 0, 100) / 100m);
        else if (discountType == DiscountType.FixedAmount && discountValue > 0)
            discount = Money.Round(Money.Clamp(discountValue, 0, subtotal));

        var taxableTotal = subtotal - discount;
        var serviceCharge = serviceChargeRatePercent <= 0
            ? 0
            : Money.Round(taxableTotal * serviceChargeRatePercent / 100m);

        var allocations = new LineCalculation[lines.Count];
        decimal taxTotal = 0;
        decimal allocatedDiscount = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            decimal share;
            if (i == lines.Count - 1)
                share = discount - allocatedDiscount; // absorb rounding on last line
            else
                share = subtotal == 0 ? 0 : Money.Round(discount * nets[i] / subtotal);
            allocatedDiscount += share;

            var baseAmount = nets[i] - share;
            var tax = Money.Round(baseAmount * Math.Max(lines[i].TaxRate, 0) / 100m);
            taxTotal += tax;
            allocations[i] = new LineCalculation(nets[i], share, baseAmount, tax,
                Money.Round(baseAmount + tax));
        }

        var total = Money.Round(subtotal - discount + taxTotal + serviceCharge);
        return new OrderTotals(subtotal, discount, Money.Round(taxTotal), serviceCharge, total, allocations);
    }

    public static decimal Change(decimal amountPaid, decimal total) =>
        amountPaid <= total ? 0 : Money.Round(amountPaid - total);
}
