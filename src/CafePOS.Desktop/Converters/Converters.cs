using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;

namespace CafePOS.Desktop.Converters;

public sealed class ImageUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var url = MediaUrl.Resolve(value as string);
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnDemand;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(url, UriKind.Absolute);
            image.EndInit();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase) || Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null && value is not string s || value is string text && !string.IsNullOrWhiteSpace(text);
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase) || Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            long l => l,
            _ => 0m
        };
        return "LKR " + amount.ToString("N2", culture);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            long l => l,
            _ => 0m
        };
        return amount.ToString("N2", culture);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value!;
}

public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            null => string.Empty,
            "DineIn" => "Dine-in",
            "Takeaway" => "Takeaway",
            "Delivery" => "Delivery",
            "BankTransfer" => "Bank transfer",
            "PartiallyReceived" => "Partially received",
            var other => SplitWords(other)
        };

    private static string SplitWords(string value)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) builder.Append(' ');
            builder.Append(value[i]);
        }
        return builder.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value!;
}

public sealed class StatusBrushConverter : IValueConverter
{
    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static readonly SolidColorBrush Slate = Brush("#64748B");
    private static readonly SolidColorBrush Amber = Brush("#D97706");
    private static readonly SolidColorBrush Blue = Brush("#2563EB");
    private static readonly SolidColorBrush Green = Brush("#15803D");
    private static readonly SolidColorBrush Teal = Brush("#0F766E");
    private static readonly SolidColorBrush Red = Brush("#DC2626");
    private static readonly SolidColorBrush Purple = Brush("#7C3AED");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        OrderStatus.Pending => Amber,
        OrderStatus.Preparing => Blue,
        OrderStatus.Ready => Green,
        OrderStatus.Served => Teal,
        OrderStatus.Completed => Slate,
        OrderStatus.Cancelled => Red,
        OrderStatus.Draft => Slate,
        TableStatus.Available => Green,
        TableStatus.Occupied => Red,
        TableStatus.Reserved => Purple,
        PurchaseOrderStatus.Draft => Slate,
        PurchaseOrderStatus.Ordered => Blue,
        PurchaseOrderStatus.PartiallyReceived => Amber,
        PurchaseOrderStatus.Received => Green,
        PurchaseOrderStatus.Cancelled => Red,
        ShiftStatus.Open => Green,
        ShiftStatus.Closed => Slate,
        PaymentStatus.Completed => Green,
        PaymentStatus.PartiallyRefunded => Amber,
        PaymentStatus.Refunded => Red,
        bool b => b ? Red : Green,
        _ => Slate
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = (SolidColorBrush)new StatusBrushConverter().Convert(value, typeof(Brush), parameter, culture);
        var light = Color.FromArgb(28, color.Color.R, color.Color.G, color.Color.B);
        var brush = new SolidColorBrush(light);
        brush.Freeze();
        return brush;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch { int i => i, System.Collections.ICollection c => c.Count, _ => 0 };
        var visible = count > 0;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusInConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? string.Empty;
        var allowed = (parameter as string ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Any(a => string.Equals(a, current, StringComparison.OrdinalIgnoreCase))
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class DateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            DateTimeOffset dto => dto.ToLocalTime().ToString("dd MMM yyyy HH:mm", culture),
            DateTime dt => dt.ToString("dd MMM yyyy HH:mm", culture),
            DateOnly date => date.ToString("dd MMM yyyy", culture),
            _ => string.Empty
        };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

