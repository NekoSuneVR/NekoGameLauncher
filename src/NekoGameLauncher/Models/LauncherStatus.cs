namespace NekoGameLauncher.Models;

public sealed class LauncherStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public int GameCount { get; set; }
    public string StatusLabel => IsInstalled ? "Detected" : "Not detected";
}
