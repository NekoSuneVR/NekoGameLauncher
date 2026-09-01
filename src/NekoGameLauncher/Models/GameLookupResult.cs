namespace NekoGameLauncher.Models;

public sealed class GameLookupResult
{
    public string GameId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SteamAppId { get; set; } = string.Empty;
    public string CheapestPrice { get; set; } = string.Empty;
    public string DealUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}
