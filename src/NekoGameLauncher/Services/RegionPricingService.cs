using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class RegionPricingService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _steamGate = new(4, 4);
    private readonly Dictionary<string, RegionalPrice?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RegionPricingService(HttpClient http)
    {
        _http = http;
        try
        {
            var region = RegionInfo.CurrentRegion;
            CountryCode = string.IsNullOrWhiteSpace(region.TwoLetterISORegionName) ? "US" : region.TwoLetterISORegionName.ToUpperInvariant();
            CurrencyCode = string.IsNullOrWhiteSpace(region.ISOCurrencySymbol) ? "USD" : region.ISOCurrencySymbol.ToUpperInvariant();
        }
        catch
        {
            CountryCode = "US";
            CurrencyCode = "USD";
        }
    }

    public string CountryCode { get; }
    public string CurrencyCode { get; }
    public string RegionDescription => $"{CountryCode} / {CurrencyCode}";

    public async Task<RegionalPrice?> GetSteamPriceAsync(string steamAppId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(steamAppId) || !steamAppId.All(char.IsDigit)) return null;
        var cacheKey = $"{CountryCode}:{steamAppId}";
        lock (_cache)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        }

        await _steamGate.WaitAsync(cancellationToken);
        try
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
            }

            var url = $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(steamAppId)}&cc={Uri.EscapeDataString(CountryCode)}&filters=price_overview";
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Cache(cacheKey, null);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty(steamAppId, out var app)
                || !app.TryGetProperty("success", out var success)
                || success.ValueKind != JsonValueKind.True
                || !app.TryGetProperty("data", out var data)
                || !data.TryGetProperty("price_overview", out var price))
            {
                Cache(cacheKey, null);
                return null;
            }

            var currency = price.TryGetProperty("currency", out var currencyElement)
                ? currencyElement.GetString() ?? CurrencyCode
                : CurrencyCode;
            var formatted = price.TryGetProperty("final_formatted", out var formattedElement)
                ? formattedElement.GetString() ?? string.Empty
                : string.Empty;
            var discount = price.TryGetProperty("discount_percent", out var discountElement) && discountElement.TryGetInt32(out var discountValue)
                ? discountValue
                : 0;

            if (string.IsNullOrWhiteSpace(formatted) && price.TryGetProperty("final", out var finalElement) && finalElement.TryGetInt64(out var minorUnits))
                formatted = FormatMinorUnits(minorUnits, currency);

            if (string.IsNullOrWhiteSpace(formatted))
            {
                Cache(cacheKey, null);
                return null;
            }

            var result = new RegionalPrice(formatted, currency.ToUpperInvariant(), CountryCode, discount);
            Cache(cacheKey, result);
            return result;
        }
        catch
        {
            Cache(cacheKey, null);
            return null;
        }
        finally
        {
            _steamGate.Release();
        }
    }

    private void Cache(string key, RegionalPrice? value)
    {
        lock (_cache) _cache[key] = value;
    }

    private static string FormatMinorUnits(long value, string currency)
    {
        var amount = value / 100m;
        try
        {
            var region = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Select(culture => new { Culture = culture, Region = TryRegion(culture.Name) })
                .FirstOrDefault(x => x.Region?.ISOCurrencySymbol.Equals(currency, StringComparison.OrdinalIgnoreCase) == true);
            if (region is not null) return amount.ToString("C", region.Culture);
        }
        catch { }
        return $"{amount:0.00} {currency}";
    }

    private static RegionInfo? TryRegion(string cultureName)
    {
        try { return new RegionInfo(cultureName); }
        catch { return null; }
    }
}

public sealed record RegionalPrice(string DisplayPrice, string CurrencyCode, string CountryCode, int DiscountPercent)
{
    public string DisplayLabel => $"{DisplayPrice} {CurrencyCode}";
}
