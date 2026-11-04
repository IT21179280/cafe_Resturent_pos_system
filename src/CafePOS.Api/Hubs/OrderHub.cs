using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CafePOS.Api.Hubs;

[Authorize]
public class OrderHub : Hub
{
    public const string CashierGroup = "cashiers";
    public const string KitchenGroup = "kitchen";
    public const string ManagerGroup = "managers";

    public Task JoinCashier() => Groups.AddToGroupAsync(Context.ConnectionId, CashierGroup);

    public Task JoinKitchen(string? station = null)
    {
        var groups = new List<Task> { Groups.AddToGroupAsync(Context.ConnectionId, KitchenGroup) };
        if (!string.IsNullOrWhiteSpace(station))
            groups.Add(Groups.AddToGroupAsync(Context.ConnectionId, $"station:{station}"));
        return Task.WhenAll(groups);
    }

    public Task JoinManagers() => Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup);

    public Task LeaveAll() => Task.WhenAll(
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CashierGroup),
        Groups.RemoveFromGroupAsync(Context.ConnectionId, KitchenGroup),
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ManagerGroup));
}
