using System.Net.Http;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace CafePOS.Desktop.Services;

/// <summary>
/// Realtime channel for the cashier, kitchen and manager screens.
/// Consumers must marshal events to the UI thread (see <see cref="Ui.OnUiThread"/>).
/// </summary>
public sealed class SignalRService : IAsyncDisposable
{
    private readonly AuthSession _session;
    private readonly AppSettingsStore _settings;
    private HubConnection? _connection;

    public SignalRService(AuthSession session, AppSettingsStore settings)
    {
        _session = session;
        _settings = settings;
    }

    public event Action<OrderDto>? OrderCreated;
    public event Action<OrderDto>? OrderUpdated;
    public event Action<OrderDto>? OrderStatusChanged;
    public event Action<bool, string>? ConnectionChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync()
    {
        if (_session.User is null || string.IsNullOrWhiteSpace(_session.AccessToken)) return;
        if (_connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting }) return;

        await DisconnectAsync();

        var root = (_settings.ServerUrl ?? "http://localhost:5080").TrimEnd('/');
        if (root.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
            root = root[..^"/api/v1".Length];
        if (!root.StartsWith("http", StringComparison.OrdinalIgnoreCase)) root = "http://" + root;

        var connection = new HubConnectionBuilder()
            .WithUrl(root + "/hubs/orders", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_session.AccessToken);
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20) })
            .Build();

        connection.On<OrderDto>("OrderCreated", dto => OrderCreated?.Invoke(dto));
        connection.On<OrderDto>("OrderUpdated", dto => OrderUpdated?.Invoke(dto));
        connection.On<OrderDto>("OrderStatusChanged", dto => OrderStatusChanged?.Invoke(dto));
        connection.Reconnecting += _ =>
        {
            ConnectionChanged?.Invoke(false, "Reconnecting…");
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            ConnectionChanged?.Invoke(true, "Live");
            return JoinGroupsAsync(connection);
        };
        connection.Closed += _ =>
        {
            ConnectionChanged?.Invoke(false, "Offline");
            return Task.CompletedTask;
        };

        _connection = connection;
        try
        {
            await connection.StartAsync();
            await JoinGroupsAsync(connection);
            ConnectionChanged?.Invoke(true, "Live");
        }
        catch
        {
            ConnectionChanged?.Invoke(false, "Offline");
        }
    }

    private async Task JoinGroupsAsync(HubConnection connection)
    {
        if (_session.User is null) return;
        try
        {
            switch (_session.User.RoleName)
            {
                case UserRole.Kitchen:
                    await connection.InvokeAsync("JoinKitchen", (string?)null);
                    break;
                case UserRole.Cashier:
                    await connection.InvokeAsync("JoinCashier");
                    break;
                default:
                    await connection.InvokeAsync("JoinManagers");
                    await connection.InvokeAsync("JoinCashier");
                    break;
            }
        }
        catch
        {
            // The next reconnect will retry group membership.
        }
    }

    public async Task JoinKitchenAsync(string? station = null)
    {
        if (_connection is not { State: HubConnectionState.Connected }) return;
        try
        {
            await _connection.InvokeAsync("JoinKitchen", station);
        }
        catch
        {
            // Ignore; the screen still refreshes manually.
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        var connection = _connection;
        _connection = null;
        try
        {
            await connection.StopAsync();
        }
        catch
        {
            // Ignore shutdown errors.
        }
        await connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
