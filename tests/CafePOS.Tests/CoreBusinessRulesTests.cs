using CafePOS.Core.BusinessRules;
using CafePOS.Core.Enums;

namespace CafePOS.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(-10.005, -10.01)]
    [InlineData(0, 0)]
    public void Round_uses_away_from_zero_midpoint(decimal input, decimal expected) =>
        Assert.Equal(expected, Money.Round(input));

    [Theory]
    [InlineData(-5, 0, 100, 0)]
    [InlineData(150, 0, 100, 100)]
    [InlineData(42.5, 0, 100, 42.5)]
    public void Clamp_limits_value(decimal value, decimal min, decimal max, decimal expected) =>
        Assert.Equal(expected, Money.Clamp(value, min, max));

    [Fact]
    public void Format_uses_lkr_and_invariant_grouping() =>
        Assert.Equal("LKR 1,234.50", Money.Format(1234.5m));
}

public class OrderPricingTests
{
    [Fact]
    public void Calculates_tax_and_total_for_simple_lines()
    {
        var lines = new[] { new PricingLine(100m, 2m, 0m, 10m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.None, 0m, 0m);

        Assert.Equal(200m, totals.Subtotal);
        Assert.Equal(0m, totals.DiscountAmount);
        Assert.Equal(20m, totals.TaxAmount);
        Assert.Equal(0m, totals.ServiceCharge);
        Assert.Equal(220m, totals.Total);
        Assert.Single(totals.Lines);
        Assert.Equal(20m, totals.Lines[0].Tax);
    }

    [Fact]
    public void Applies_item_discount_before_tax()
    {
        var lines = new[] { new PricingLine(100m, 2m, 10m, 10m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.None, 0m, 0m);

        Assert.Equal(190m, totals.Subtotal);
        Assert.Equal(19m, totals.TaxAmount);
        Assert.Equal(209m, totals.Total);
    }

    [Fact]
    public void Includes_modifier_total_in_line_net()
    {
        var lines = new[] { new PricingLine(50m, 1m, 0m, 0m, ModifierTotal: 20m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.None, 0m, 0m);

        Assert.Equal(70m, totals.Subtotal);
        Assert.Equal(70m, totals.Total);
    }

    [Fact]
    public void Allocates_percentage_discount_pro_rata_before_tax()
    {
        var lines = new[]
        {
            new PricingLine(100m, 2m, 10m, 10m),
            new PricingLine(50m, 1m, 0m, 0m)
        };

        var totals = OrderPricing.Calculate(lines, DiscountType.Percentage, 10m, 5m);

        Assert.Equal(240m, totals.Subtotal);
        Assert.Equal(24m, totals.DiscountAmount);
        Assert.Equal(19m, totals.Lines[0].DiscountShare);
        Assert.Equal(5m, totals.Lines[1].DiscountShare);
        Assert.Equal(17.10m, totals.TaxAmount);
        Assert.Equal(10.80m, totals.ServiceCharge);
        Assert.Equal(243.90m, totals.Total);
    }

    [Fact]
    public void Allocates_rounding_remainder_to_last_line()
    {
        var lines = new[]
        {
            new PricingLine(1m, 1m, 0m, 0m),
            new PricingLine(1m, 1m, 0m, 0m),
            new PricingLine(1m, 1m, 0m, 0m)
        };

        var totals = OrderPricing.Calculate(lines, DiscountType.FixedAmount, 0.01m, 0m);

        Assert.Equal(0.01m, totals.DiscountAmount);
        Assert.Equal(totals.DiscountAmount, totals.Lines.Sum(l => l.DiscountShare));
        Assert.Equal(0.01m, totals.Lines[^1].DiscountShare);
    }

    [Fact]
    public void Clamps_fixed_discount_to_subtotal()
    {
        var lines = new[] { new PricingLine(100m, 1m, 0m, 0m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.FixedAmount, 500m, 0m);

        Assert.Equal(100m, totals.DiscountAmount);
        Assert.Equal(0m, totals.TaxableBase());
        Assert.Equal(0m, totals.Total);
    }

    [Fact]
    public void Clamps_percentage_to_hundred()
    {
        var lines = new[] { new PricingLine(100m, 1m, 0m, 0m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.Percentage, 150m, 0m);

        Assert.Equal(100m, totals.DiscountAmount);
        Assert.Equal(0m, totals.Total);
    }

    [Fact]
    public void Ignores_zero_and_negative_discounts_and_rates()
    {
        var lines = new[] { new PricingLine(100m, 1m, 0m, -5m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.Percentage, -10m, -5m);

        Assert.Equal(0m, totals.DiscountAmount);
        Assert.Equal(0m, totals.TaxAmount);
        Assert.Equal(0m, totals.ServiceCharge);
        Assert.Equal(100m, totals.Total);
    }

    [Fact]
    public void Line_discount_cannot_make_net_negative()
    {
        var lines = new[] { new PricingLine(10m, 1m, 50m, 10m) };

        var totals = OrderPricing.Calculate(lines, DiscountType.None, 0m, 0m);

        Assert.Equal(0m, totals.Subtotal);
        Assert.Equal(0m, totals.Total);
    }

    [Fact]
    public void Empty_order_returns_zero_totals()
    {
        var totals = OrderPricing.Calculate(Array.Empty<PricingLine>(), DiscountType.Percentage, 10m, 10m);

        Assert.Equal(0m, totals.Total);
        Assert.Empty(totals.Lines);
    }

    [Theory]
    [InlineData(2500, 2000, 0)]
    [InlineData(2500, 2500, 0)]
    [InlineData(2500, 3000, 500)]
    public void Change_is_only_returned_when_overpaid(decimal total, decimal paid, decimal expected) =>
        Assert.Equal(expected, OrderPricing.Change(paid, total));
}

public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Draft, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Served, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Pending, false)]
    public void Validates_transitions(OrderStatus from, OrderStatus to, bool expected) =>
        Assert.Equal(expected, OrderStateMachine.CanTransition(from, to));

    [Fact]
    public void Same_status_is_idempotent()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
            Assert.True(OrderStateMachine.CanTransition(status, status));
    }

    [Fact]
    public void EnsureCanTransition_throws_for_invalid_transition()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OrderStateMachine.EnsureCanTransition(OrderStatus.Completed, OrderStatus.Preparing));
    }

    [Theory]
    [InlineData(OrderStatus.Draft, true)]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Preparing, false)]
    [InlineData(OrderStatus.Completed, false)]
    public void Editability_only_for_draft_and_pending(OrderStatus status, bool expected) =>
        Assert.Equal(expected, OrderStateMachine.IsEditable(status));

    [Theory]
    [InlineData(OrderStatus.Draft, true)]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Served, true)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Cancellability_excludes_final_states(OrderStatus status, bool expected) =>
        Assert.Equal(expected, OrderStateMachine.IsCancellable(status));

    [Fact]
    public void Only_completed_counts_as_paid()
    {
        Assert.True(OrderStateMachine.IsPaid(OrderStatus.Completed));
        Assert.False(OrderStateMachine.IsPaid(OrderStatus.Served));
    }
}

public class ShiftMathTests
{
    [Fact]
    public void Expected_cash_includes_sales_refunds_and_expenses() =>
        Assert.Equal(6850m, ShiftMath.ExpectedCash(5000m, 2000m, 100m, 50m));

    [Fact]
    public void Expected_cash_rounds_to_two_decimals() =>
        Assert.Equal(10.01m, ShiftMath.ExpectedCash(10m, 0.005m, 0m, 0m));

    [Theory]
    [InlineData(6900, 6900, 0)]
    [InlineData(6890.5, 6900, -9.5)]
    [InlineData(6912.34, 6900, 12.34)]
    public void Difference_is_counted_minus_expected(decimal counted, decimal expected, decimal difference) =>
        Assert.Equal(difference, ShiftMath.Difference(counted, expected));
}

public class InventoryPolicyTests
{
    [Theory]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Served, false)]
    [InlineData(OrderStatus.Preparing, false)]
    public void Default_deducts_when_completed(OrderStatus status, bool expected) =>
        Assert.Equal(expected, InventoryPolicy.ShouldDeduct(status, null));

    [Theory]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Served, true)]
    [InlineData(OrderStatus.Ready, false)]
    public void Served_setting_deducts_on_serve_or_complete(OrderStatus status, bool expected) =>
        Assert.Equal(expected, InventoryPolicy.ShouldDeduct(status, "Served"));

    [Theory]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Served, false)]
    [InlineData(OrderStatus.Preparing, false)]
    public void Paid_setting_deducts_only_when_completed(OrderStatus status, bool expected) =>
        Assert.Equal(expected, InventoryPolicy.ShouldDeduct(status, "Paid"));

    [Theory]
    [InlineData("Completed")]
    [InlineData("Served")]
    [InlineData("Paid")]
    [InlineData("something-else")]
    public void Stock_return_matches_deduction_policy(string setting)
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
            Assert.Equal(InventoryPolicy.ShouldDeduct(status, setting), InventoryPolicy.ShouldReturnStock(status, setting));
    }

    [Fact]
    public void Blank_setting_uses_completed_default() =>
        Assert.True(InventoryPolicy.ShouldDeduct(OrderStatus.Completed, "   "));
}

internal static class OrderTotalsExtensions
{
    public static decimal TaxableBase(this OrderTotals totals) => totals.Subtotal - totals.DiscountAmount;
}
