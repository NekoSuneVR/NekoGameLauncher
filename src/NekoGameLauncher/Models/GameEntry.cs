namespace NekoGameLauncher.Models;

public sealed class GameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Launcher { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC";
    public string SourceId { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string LaunchTarget { get; set; } = string.Empty;
    public string LaunchArguments { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public bool IsInstalled { get; set; } = true;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastPlayedAt { get; set; }

    public bool CanLaunch => !string.IsNullOrWhiteSpace(LaunchTarget);
}
