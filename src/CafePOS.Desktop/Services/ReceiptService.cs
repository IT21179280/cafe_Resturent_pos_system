using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CafePOS.Core.Contracts;

namespace CafePOS.Desktop.Services;

/// <summary>
/// Builds a clean 80mm receipt and can print it either to a thermal printer
/// (raw ESC/POS) or through the standard Windows print dialog.
/// </summary>
public sealed class ReceiptService
{
    private readonly AppSettingsStore _settings;
    private readonly BrandingService _branding;

    static ReceiptService() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public ReceiptService(AppSettingsStore settings, BrandingService branding)
    {
        _settings = settings;
        _branding = branding;
    }

    public bool HasKitchenPrinter =>
        _settings.UseKitchenPrinter && !string.IsNullOrWhiteSpace(_settings.KitchenPrinterName);

    public string BuildReceipt(OrderDto order, AppSettingsDto? settings)
    {
        const int width = 42;
        var builder = new StringBuilder();
        var name = RestaurantName(settings);
        builder.AppendLine(Center(name.ToUpperInvariant(), width));
        if (!string.IsNullOrWhiteSpace(settings?.RestaurantAddress))
            builder.AppendLine(Center(settings!.RestaurantAddress, width));
        if (!string.IsNullOrWhiteSpace(settings?.RestaurantPhone))
            builder.AppendLine(Center(settings!.RestaurantPhone, width));
        builder.AppendLine(new string('-', width));
        builder.AppendLine($"Order  : {order.OrderNumber}");
        builder.AppendLine($"Date   : {order.PlacedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        builder.AppendLine($"Type   : {Display(order.Type)}");
        if (!string.IsNullOrWhiteSpace(order.TableNumber)) builder.AppendLine($"Table  : {order.TableNumber}");
        if (!string.IsNullOrWhiteSpace(order.CustomerName)) builder.AppendLine($"Customer: {order.CustomerName}");
        builder.AppendLine($"Cashier: {order.CashierName}");
        builder.AppendLine(new string('-', width));

        foreach (var item in order.Items)
        {
            builder.AppendLine($"{Trim(item.ProductName, width - 14)}");
            var amount = item.LineTotal.ToString("N2");
            var qty = $"  {item.Quantity:0.##} x {item.UnitPrice:N2}";
            builder.AppendLine($"{Trim(qty, width - amount.Length - 1)}{amount.PadLeft(amount.Length + 1)}");
            foreach (var modifier in item.Modifiers)
            {
                if (modifier.Price == 0) builder.AppendLine($"    + {modifier.Name}");
                else builder.AppendLine($"    + {modifier.Name} ({modifier.Price:N2})");
            }
            if (!string.IsNullOrWhiteSpace(item.Notes)) builder.AppendLine($"    * {item.Notes}");
        }

        builder.AppendLine(new string('-', width));
        builder.AppendLine(Row("Subtotal", order.Subtotal, width));
        if (order.DiscountAmount > 0) builder.AppendLine(Row("Discount", -order.DiscountAmount, width));
        if (order.TaxAmount > 0) builder.AppendLine(Row("Tax", order.TaxAmount, width));
        if (order.ServiceCharge > 0) builder.AppendLine(Row("Service charge", order.ServiceCharge, width));
        builder.AppendLine(Row("TOTAL", order.Total, width));
        builder.AppendLine();

        foreach (var payment in order.Payments)
            builder.AppendLine(Row($"{Display(payment.Method)}", payment.Amount, width));
        if (order.ChangeDue > 0) builder.AppendLine(Row("Change", order.ChangeDue, width));
        builder.AppendLine(Row("Paid", order.PaidAmount, width));
        builder.AppendLine(new string('-', width));
        builder.AppendLine(Center(settings?.ReceiptFooter ?? "Thank you. Please come again!", width));
        builder.AppendLine(Center("Powered by CafePOS", width));
        return builder.ToString();
    }

    /// <summary>
    /// Builds an 80mm kitchen ticket. Prices are intentionally omitted; only the
    /// station, quantities, modifiers and preparation notes are printed.
    /// </summary>
    public string BuildKitchenTicket(OrderDto order, AppSettingsDto? settings)
    {
        const int width = 42;
        var builder = new StringBuilder();
        builder.AppendLine(Center("KITCHEN TICKET", width));
        var name = RestaurantName(settings);
        if (!string.IsNullOrWhiteSpace(name)) builder.AppendLine(Center(name.ToUpperInvariant(), width));
        builder.AppendLine(new string('=', width));
        builder.AppendLine($"Order : {order.OrderNumber}");
        builder.AppendLine($"Time  : {order.PlacedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        builder.AppendLine($"Type  : {Display(order.Type)}");
        if (!string.IsNullOrWhiteSpace(order.TableNumber)) builder.AppendLine($"Table : {order.TableNumber}");
        if (!string.IsNullOrWhiteSpace(order.CustomerName)) builder.AppendLine($"Name  : {order.CustomerName}");
        builder.AppendLine(new string('-', width));

        var stations = order.Items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.PreparationStation.ToString())
                ? "Kitchen"
                : item.PreparationStation.ToString())
            .OrderBy(group => group.Key);

        foreach (var station in stations)
        {
            builder.AppendLine($"[{station.Key.ToUpperInvariant()}]");
            foreach (var item in station)
            {
                builder.AppendLine($"{item.Quantity:0.##} x {Trim(item.ProductName, width - 6)}");
                foreach (var modifier in item.Modifiers)
                    builder.AppendLine($"    + {modifier.Name}");
                if (!string.IsNullOrWhiteSpace(item.Notes)) builder.AppendLine($"    * {item.Notes}");
            }
            builder.AppendLine();
        }

        builder.AppendLine(new string('-', width));
        builder.AppendLine($"Items : {order.Items.Sum(item => item.Quantity):0.##}");
        if (!string.IsNullOrWhiteSpace(order.Notes)) builder.AppendLine($"Note  : {order.Notes}");
        builder.AppendLine(Center("Prepare immediately", width));
        return builder.ToString();
    }

    /// <summary>Printing is intentionally best-effort; a printer problem must never block a sale.</summary>
    public (bool Printed, string? Error) Print(OrderDto order, AppSettingsDto? settings, bool useDialog = false)
    {
        var text = BuildReceipt(order, settings);
        var logo = LoadLogo(LogoUrl(settings));
        if (!useDialog && _settings.UseThermalPrinter && !string.IsNullOrWhiteSpace(_settings.ThermalPrinterName))
        {
            if (TryPrintRaw(_settings.ThermalPrinterName, text, "CafePOS Receipt", logo)) return (true, null);
        }

        return PrintDocument(text, $"CafePOS {order.OrderNumber}", logo);
    }

    /// <summary>
    /// Prints the kitchen ticket to the dedicated kitchen printer when configured,
    /// otherwise falls back to the receipt printer or the Windows print dialog.
    /// </summary>
    public (bool Printed, string? Error) PrintKitchenTicket(OrderDto order, AppSettingsDto? settings, bool useDialog = false)
    {
        var text = BuildKitchenTicket(order, settings);
        var logo = LoadLogo(LogoUrl(settings));
        if (!useDialog && _settings.UseKitchenPrinter && !string.IsNullOrWhiteSpace(_settings.KitchenPrinterName))
        {
            if (TryPrintRaw(_settings.KitchenPrinterName, text, "CafePOS Kitchen Ticket", logo)) return (true, null);
        }
        if (!useDialog && _settings.UseThermalPrinter && !string.IsNullOrWhiteSpace(_settings.ThermalPrinterName))
        {
            if (TryPrintRaw(_settings.ThermalPrinterName, text, "CafePOS Kitchen Ticket", logo)) return (true, null);
        }

        return PrintDocument(text, $"CafePOS KOT {order.OrderNumber}", logo);
    }

    private static (bool Printed, string? Error) PrintDocument(string text, string documentName, BitmapSource? logo)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return (false, "Printing was cancelled.");
            var document = BuildFlowDocument(text, dialog, logo);
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, documentName);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static FlowDocument BuildFlowDocument(string text, PrintDialog dialog, BitmapSource? logo)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            PageWidth = dialog.PrintableAreaWidth,
            PageHeight = dialog.PrintableAreaHeight,
            ColumnWidth = dialog.PrintableAreaWidth,
            PagePadding = new Thickness(18)
        };

        if (logo is not null)
        {
            var width = Math.Max(60, Math.Min(190, dialog.PrintableAreaWidth - 36));
            var image = new Image
            {
                Source = logo,
                Width = width,
                MaxHeight = 120,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var block = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            block.Inlines.Add(new InlineUIContainer(image));
            document.Blocks.Add(block);
        }

        document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0), LineHeight = 15 });
        return document;
    }

    private static bool TryPrintRaw(string printerName, string text, string documentName, BitmapSource? logo)
    {
        IntPtr handle = IntPtr.Zero;
        IntPtr document = IntPtr.Zero;
        try
        {
            if (!OpenPrinter(printerName, out handle, IntPtr.Zero)) return false;
            var docInfo = new DocInfo { DocumentName = documentName, DataType = "RAW" };
            document = StartDocPrinter(handle, 1, docInfo);
            if (document == IntPtr.Zero) return false;
            if (!StartPagePrinter(handle)) return false;

            using var body = new MemoryStream();
            body.WriteByte(0x1B);
            body.WriteByte(0x40); // ESC @ initialize

            if (logo is not null)
            {
                var raster = BuildEscPosRaster(logo);
                if (raster.Length > 0)
                {
                    var center = new byte[] { 0x1B, 0x61, 0x01 };
                    body.Write(center, 0, center.Length);
                    body.Write(raster, 0, raster.Length);
                    var left = new byte[] { 0x1B, 0x61, 0x00 };
                    body.Write(left, 0, left.Length);
                }
            }

            var textBytes = Encoding.GetEncoding(437).GetBytes(text.Replace("\r\n", "\n") + "\n\n\n");
            body.Write(textBytes, 0, textBytes.Length);
            var cut = new byte[] { 0x1D, 0x56, 0x41, 0x03 }; // feed and cut
            body.Write(cut, 0, cut.Length);

            var bytes = body.ToArray();
            if (!WritePrinter(handle, bytes, bytes.Length, out var written) || written != bytes.Length) return false;
            EndPagePrinter(handle);
            EndDocPrinter(handle);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (document != IntPtr.Zero) { try { EndDocPrinter(handle); } catch { } }
            if (handle != IntPtr.Zero) { try { ClosePrinter(handle); } catch { } }
        }
    }

    /// <summary>
    /// Converts the configured logo to a monochrome ESC/POS raster image
    /// (GS v 0). The width is capped so the logo fits an 80mm thermal printer.
    /// </summary>
    private static byte[] BuildEscPosRaster(BitmapSource source)
    {
        try
        {
            var scaled = ScaleLogo(source, 384, 254);
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            if (width <= 0 || height <= 0) return Array.Empty<byte>();

            var stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            var widthBytes = (width + 7) / 8;
            var raster = new byte[widthBytes * height];
            for (var y = 0; y < height; y++)
            {
                var inputRow = y * stride;
                var outputRow = y * widthBytes;
                for (var x = 0; x < width; x++)
                {
                    var index = inputRow + x * 4;
                    if (pixels[index + 3] < 32) continue;
                    var luminance = (pixels[index + 2] * 299 + pixels[index + 1] * 587 + pixels[index] * 114) / 1000;
                    if (luminance < 160)
                        raster[outputRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }

            var header = new byte[]
            {
                0x1D, 0x76, 0x30, 0x00,
                (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
                (byte)(height & 0xFF), (byte)((height >> 8) & 0xFF)
            };
            var result = new byte[header.Length + raster.Length + 1];
            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(raster, 0, result, header.Length, raster.Length);
            result[^1] = 0x0A;
            return result;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static BitmapSource ScaleLogo(BitmapSource source, int maxWidth, int maxHeight)
    {
        var scale = 1.0;
        if (source.PixelWidth > maxWidth)
            scale = Math.Min(scale, maxWidth / (double)source.PixelWidth);
        if (source.PixelHeight > maxHeight)
            scale = Math.Min(scale, maxHeight / (double)source.PixelHeight);
        if (scale >= 1) return source;

        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    private static BitmapSource? LoadLogo(string? url)
    {
        var resolved = MediaUrl.Resolve(url);
        if (string.IsNullOrWhiteSpace(resolved)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(resolved, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private string RestaurantName(AppSettingsDto? settings)
    {
        if (!string.IsNullOrWhiteSpace(settings?.RestaurantName)) return settings!.RestaurantName;
        if (!string.IsNullOrWhiteSpace(_branding.RestaurantName)) return _branding.RestaurantName;
        if (!string.IsNullOrWhiteSpace(_branding.SystemName)) return _branding.SystemName;
        return "CafePOS";
    }

    private string? LogoUrl(AppSettingsDto? settings) =>
        !string.IsNullOrWhiteSpace(settings?.LogoUrl) ? settings!.LogoUrl : _branding.LogoUrl;

    private static string Display(object value) => (value.ToString() ?? string.Empty) switch
    {
        "DineIn" => "Dine-in",
        "Takeaway" => "Takeaway",
        "BankTransfer" => "Bank transfer",
        var other => other
    };

    private static string Row(string label, decimal amount, int width)
    {
        var value = amount.ToString("N2");
        if (label.Length + value.Length + 1 > width)
            return label + Environment.NewLine + value.PadLeft(width);
        return label + new string(' ', Math.Max(1, width - label.Length - value.Length)) + value;
    }

    private static string Center(string text, int width)
    {
        text = text.Trim();
        if (text.Length >= width) return text;
        var left = (width - text.Length) / 2;
        return new string(' ', left) + text;
    }

    private static string Trim(string text, int length) =>
        text.Length <= length ? text : text[..Math.Max(0, length)];

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName = string.Empty;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType = "RAW";
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr StartDocPrinter(IntPtr handle, int level, DocInfo document);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, byte[] buffer, int length, out int written);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);
}
