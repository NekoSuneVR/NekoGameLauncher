namespace NekoGameLauncher.Models;

public sealed class AppSettings
{
    public bool GamerPowerEnabled { get; set; } = true;
    public bool CheapSharkEnabled { get; set; } = true;
    public bool AutoGamingModeEnabled { get; set; }
    public bool BoostGamePriorityEnabled { get; set; } = true;
    public List<CustomDealEndpoint> CustomDealEndpoints { get; set; } = [];
}

public sealed class CustomDealEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
