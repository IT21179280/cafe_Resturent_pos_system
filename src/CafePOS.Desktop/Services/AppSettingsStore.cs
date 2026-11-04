using System.IO;
using System.Text.Json;

namespace CafePOS.Desktop.Services;

public class AppSettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CafePOS");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public string ServerUrl { get; set; } = "http://localhost:5080";
    public string ThermalPrinterName { get; set; } = string.Empty;
    public bool UseThermalPrinter { get; set; }
    public string KitchenPrinterName { get; set; } = string.Empty;
    public bool UseKitchenPrinter { get; set; }
    public string LastEmail { get; set; } = string.Empty;

    public static AppSettingsStore Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettingsStore>(json) ?? new AppSettingsStore();
            }
        }
        catch
        {
            // A corrupt settings file must never stop the POS from starting.
        }
        return new AppSettingsStore();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
