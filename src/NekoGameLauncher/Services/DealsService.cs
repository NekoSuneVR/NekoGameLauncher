using NekoGameLauncher.Models;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class DealsService
{
    private readonly HttpClient _http;

    public DealsService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NekoGameLauncher/0.1 (+https://github.com/NekoSuneVR/NekoGameLauncher)");
    }

    public async Task<IReadOnlyList<DealOffer>> GetOffersAsync(AppSettings settings, string? query, bool freeOnly, CancellationToken cancellationToken = default)
    {
        var offers = new List<DealOffer>();
        if (settings.GamerPowerEnabled)
        {
            try { offers.AddRange(await GetGamerPowerAsync(query, cancellationToken)); } catch { }
        }
        if (settings.CheapSharkEnabled)
        {
            try { offers.AddRange(await GetCheapSharkDealsAsync(query, cancellationToken)); } catch { }
        }
        foreach (var endpoint in settings.CustomDealEndpoints.Where(e => e.Enabled))
        {
            try { offers.AddRange(await GetCustomAsync(endpoint, query, cancellationToken)); } catch { }
        }

        if (freeOnly) offers = offers.Where(o => o.IsFree).ToList();
        return offers
            .GroupBy(o => $"{o.Source}|{o.Id}|{o.Title}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(o => o.IsFree)
            .ThenByDescending(o => o.SavingsPercent)
            .ThenBy(o => o.Title)
            .Take(150)
            .ToList();
    }

    public async Task<IReadOnlyList<GameLookupResult>> SearchGamesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var url = $"https://www.cheapshark.com/api/1.0/games?title={Uri.EscapeDataString(query)}&limit=40";
        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var results = new List<GameLookupResult>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dealId = GetString(item, "cheapestDealID");
            results.Add(new GameLookupResult
            {
                GameId = GetString(item, "gameID"),
                Name = GetString(item, "external"),
                SteamAppId = GetString(item, "steamAppID"),
                CheapestPrice = GetString(item, "cheapest"),
                DealUrl = string.IsNullOrWhiteSpace(dealId) ? string.Empty : $"https://www.cheapshark.com/redirect?dealID={dealId}",
                ThumbnailUrl = GetString(item, "thumb")
            });
        }
        return results;
    }

    private async Task<IEnumerable<DealOffer>> GetGamerPowerAsync(string? query, CancellationToken ct)
    {
        using var response = await _http.GetAsync("https://www.gamerpower.com/api/giveaways?type=game", ct);
        if ((int)response.StatusCode == 201) return [];
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = new List<DealOffer>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var title = GetString(item, "title");
            if (!string.IsNullOrWhiteSpace(query) && !title.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new DealOffer
            {
                Id = GetString(item, "id"),
                Title = title,
                Store = GetString(item, "platforms"),
                Source = "GamerPower",
                SalePrice = "0.00",
                NormalPrice = GetString(item, "worth").TrimStart('$'),
                SavingsPercent = 100,
                IsFree = true,
                DealUrl = GetString(item, "open_giveaway_url"),
                ThumbnailUrl = GetString(item, "thumbnail"),
                Description = GetString(item, "description"),
                EndsAt = GetString(item, "end_date")
            });
        }
        return result;
    }

    private async Task<IEnumerable<DealOffer>> GetCheapSharkDealsAsync(string? query, CancellationToken ct)
    {
        var url = "https://www.cheapshark.com/api/1.0/deals?onSale=1&pageSize=60&sortBy=DealRating";
        if (!string.IsNullOrWhiteSpace(query)) url += $"&title={Uri.EscapeDataString(query)}";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = new List<DealOffer>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var sale = GetString(item, "salePrice");
            _ = decimal.TryParse(GetString(item, "savings"), NumberStyles.Float, CultureInfo.InvariantCulture, out var savings);
            _ = decimal.TryParse(sale, NumberStyles.Float, CultureInfo.InvariantCulture, out var saleValue);
            var dealId = GetString(item, "dealID");
            result.Add(new DealOffer
            {
                Id = dealId,
                Title = GetString(item, "title"),
                Store = $"Store {GetString(item, "storeID")}",
                Source = "CheapShark",
                SalePrice = sale,
                NormalPrice = GetString(item, "normalPrice"),
                SavingsPercent = Math.Round(savings, 1),
                IsFree = saleValue == 0,
                DealUrl = string.IsNullOrWhiteSpace(dealId) ? string.Empty : $"https://www.cheapshark.com/redirect?dealID={dealId}",
                ThumbnailUrl = GetString(item, "thumb")
            });
        }
        return result;
    }

    private async Task<IEnumerable<DealOffer>> GetCustomAsync(CustomDealEndpoint endpoint, string? query, CancellationToken ct)
    {
        if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return [];
        using var response = await _http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var array = ResolveArray(doc.RootElement);
        if (array is null) return [];
        var result = new List<DealOffer>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var title = GetFirst(item, "title", "name", "game");
            if (string.IsNullOrWhiteSpace(title)) continue;
            if (!string.IsNullOrWhiteSpace(query) && !title.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            var sale = GetFirst(item, "salePrice", "sale_price", "price");
            var normal = GetFirst(item, "normalPrice", "normal_price", "retailPrice", "worth");
            var freeText = GetFirst(item, "isFree", "is_free", "free");
            var isFree = freeText.Equals("true", StringComparison.OrdinalIgnoreCase) || sale is "0" or "0.0" or "0.00";
            result.Add(new DealOffer
            {
                Id = GetFirst(item, "id", "dealId", "deal_id", "slug"),
                Title = title,
                Store = GetFirst(item, "store", "platform", "platforms"),
                Source = endpoint.Name,
                SalePrice = sale,
                NormalPrice = normal,
                IsFree = isFree,
                SavingsPercent = ParseDecimal(GetFirst(item, "savings", "discount", "discountPercent")),
                DealUrl = GetFirst(item, "url", "dealUrl", "deal_url", "open_giveaway_url"),
                ThumbnailUrl = GetFirst(item, "thumbnail", "thumbnailUrl", "image"),
                Description = GetFirst(item, "description", "summary"),
                EndsAt = GetFirst(item, "endsAt", "end_date", "expires")
            });
        }
        return result;
    }

    private static JsonElement? ResolveArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var key in new[] { "deals", "offers", "games", "data", "results" })
            if (root.TryGetProperty(key, out var child) && child.ValueKind == JsonValueKind.Array) return child;
        return null;
    }

    private static string GetFirst(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value)) return ValueToString(value);
        return string.Empty;
    }

    private static string GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? ValueToString(value) : string.Empty;

    private static string ValueToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => string.Empty
    };

    private static decimal ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
