namespace CafePOS.Core.BusinessRules;

public static class Money
{
    public const int Decimals = 2;

    public static decimal Round(decimal value) =>
        Math.Round(value, Decimals, MidpointRounding.AwayFromZero);

    public static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(Math.Max(value, min), max);

    public static string Format(decimal value, string currency = "LKR") =>
        $"{currency} {value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}";
}
