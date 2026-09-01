namespace NekoGameLauncher.Models;

public sealed class GameActivityStats
{
    public string Key { get; set; } = string.Empty;
    public long TotalPlayTimeSeconds { get; set; }
    public int LaunchCount { get; set; }
    public DateTimeOffset? LastPlayedAt { get; set; }
}

public sealed class GameActivityUpdate
{
    public int ActiveGameCount { get; init; }
    public string ActiveGameName { get; init; } = string.Empty;
    public IReadOnlyList<int> GameProcessIds { get; init; } = [];
}

public sealed class SystemPerformanceSnapshot
{
    public double CpuPercent { get; init; }
    public double MemoryUsedGb { get; init; }
    public double MemoryTotalGb { get; init; }
    public double MemoryPercent { get; init; }
}
