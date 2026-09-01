using System.Globalization;

namespace NekoGameLauncher.Models;

public sealed class DealOffer
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SalePrice { get; set; } = string.Empty;
    public string NormalPrice { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SavingsPercent { get; set; }
    public bool IsFree { get; set; }
    public string DealUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;

    public string PriceLabel => IsFree ? "FREE" : FormatPrice(SalePrice, CurrencyCode);

    private static string FormatPrice(string value, string currency)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Offer";
        if (string.IsNullOrWhiteSpace(currency)) return value;

        var clean = value.Trim();
        if (clean.EndsWith(currency, StringComparison.OrdinalIgnoreCase)) return clean;
        if (currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.TrimStart('$');
            return decimal.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
                ? $"${amount:0.00} USD"
                : $"${clean} USD";
        }
        return $"{clean} {currency.ToUpperInvariant()}";
    }
}
