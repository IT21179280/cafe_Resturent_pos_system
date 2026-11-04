using System.Data;
using System.Globalization;
using System.Text.Json;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CafePOS.Desktop.ViewModels;

public sealed record ReportOption(string Key, string Label);

public partial class ReportsViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;

    public ReportsViewModel(ApiClient api)
    {
        _api = api;
        Reports = new List<ReportOption>
        {
            new("sales", "Sales by day"),
            new("sales-cashier", "Sales by cashier"),
            new("sales-payment", "Sales by payment method"),
            new("sales-category", "Sales by category"),
            new("best-sellers", "Best sellers"),
            new("low-stock", "Low stock"),
            new("inventory-movements", "Inventory movements"),
            new("wastage", "Wastage"),
            new("refunds", "Refunds"),
            new("cancellations", "Cancellations"),
            new("shifts", "Shift summaries"),
            new("gross-profit", "Gross profit")
        };
        SelectedReport = Reports[0];
    }

    public List<ReportOption> Reports { get; }

    [ObservableProperty] private ReportOption? _selectedReport;
    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;
    [ObservableProperty] private int _take = 20;
    [ObservableProperty] private DataView? _rows;
    [ObservableProperty] private DataView? _auditRows;
    [ObservableProperty] private string _rowCountLabel = "No data loaded";
    [ObservableProperty] private string _auditCountLabel = "No audit data loaded";

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        if (SelectedReport is null) return;
        var (from, to) = Range();
        var json = await _api.GetReportAsync(SelectedReport.Key, from, to, Take);
        var table = ToTable(json);
        Rows = table.DefaultView;
        RowCountLabel = $"{table.Rows.Count} row{(table.Rows.Count == 1 ? string.Empty : "s")} · {SelectedReport.Label}";
    });

    [RelayCommand]
    private async Task LoadAuditAsync() => await RunAsync(async () =>
    {
        var (from, to) = Range();
        var json = await _api.GetAuditLogAsync(from, to);
        var table = ToTable(json);
        AuditRows = table.DefaultView;
        AuditCountLabel = $"{table.Rows.Count} audit entr{(table.Rows.Count == 1 ? "y" : "ies")}";
    });

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (SelectedReport is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Export report",
            FileName = $"{SelectedReport.Key}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            var (from, to) = Range();
            var payload = await _api.ExportReportCsvAsync(SelectedReport.Key, from, to, Take);
            payload.SaveAs(dialog.FileName);
            StatusMessage = $"Report exported to {dialog.FileName}.";
        });
    }

    private (DateTimeOffset? From, DateTimeOffset? To) Range()
    {
        DateTimeOffset? from = FromDate.HasValue
            ? new DateTimeOffset(FromDate.Value.Date, TimeZoneInfo.Local.GetUtcOffset(FromDate.Value.Date))
            : null;
        DateTimeOffset? to = ToDate.HasValue
            ? new DateTimeOffset(ToDate.Value.Date.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(ToDate.Value.Date))
            : null;
        return (from, to);
    }

    private static DataTable ToTable(JsonElement root)
    {
        var table = new DataTable();
        JsonElement rows = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items))
            rows = items;
        if (rows.ValueKind != JsonValueKind.Array) return table;

        var columns = new List<string>();
        foreach (var row in rows.EnumerateArray())
            foreach (var property in row.EnumerateObject())
                if (!columns.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    columns.Add(property.Name);

        foreach (var column in columns) table.Columns.Add(column, typeof(string));

        foreach (var row in rows.EnumerateArray())
        {
            var dataRow = table.NewRow();
            foreach (var column in columns)
            {
                if (row.TryGetProperty(column, out var value))
                    dataRow[column] = Format(value);
            }
            table.Rows.Add(dataRow);
        }
        return table;
    }

    private static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.String => FormatString(value.GetString()),
        JsonValueKind.Number => value.TryGetDecimal(out var number) ? number.ToString("0.###") : value.ToString(),
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(Format)),
        _ => value.ToString()
    };

    private static string FormatString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToLocalTime().ToString("dd MMM yyyy HH:mm");
        return text;
    }
}
