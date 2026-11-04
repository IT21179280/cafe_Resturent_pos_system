using CafePOS.Core.Enums;

namespace CafePOS.Core.BusinessRules;

public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Draft] = new[] { OrderStatus.Pending, OrderStatus.Cancelled },
        [OrderStatus.Pending] = new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served, OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new[] { OrderStatus.Ready, OrderStatus.Served, OrderStatus.Cancelled },
        [OrderStatus.Ready] = new[] { OrderStatus.Served, OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Served] = new[] { OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Completed] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var list) && list.Contains(to));

    public static void EnsureCanTransition(OrderStatus from, OrderStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Order cannot move from {from} to {to}.");
    }

    public static bool IsEditable(OrderStatus status) =>
        status is OrderStatus.Draft or OrderStatus.Pending;

    public static bool IsCancellable(OrderStatus status) =>
        status is not (OrderStatus.Completed or OrderStatus.Cancelled);

    public static bool IsPaid(OrderStatus status) => status == OrderStatus.Completed;
}
