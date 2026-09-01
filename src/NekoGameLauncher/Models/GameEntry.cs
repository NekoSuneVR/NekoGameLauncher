using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NekoGameLauncher.Models;

public sealed class GameEntry : INotifyPropertyChanged
{
    private long _totalPlayTimeSeconds;
    private long _currentSessionSeconds;
    private int _launchCount;
    private bool _isRunning;
    private double _cpuUsagePercent;
    private double _memoryMb;

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

    public long TotalPlayTimeSeconds
    {
        get => _totalPlayTimeSeconds;
        set { if (Set(ref _totalPlayTimeSeconds, value)) OnPropertyChanged(nameof(PlayTimeLabel)); }
    }

    public int LaunchCount
    {
        get => _launchCount;
        set => Set(ref _launchCount, value);
    }

    [JsonIgnore]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (!Set(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(SessionLabel));
            OnPropertyChanged(nameof(PerformanceLabel));
        }
    }

    [JsonIgnore]
    public long CurrentSessionSeconds
    {
        get => _currentSessionSeconds;
        set
        {
            if (!Set(ref _currentSessionSeconds, value)) return;
            OnPropertyChanged(nameof(SessionLabel));
        }
    }

    [JsonIgnore]
    public double CpuUsagePercent
    {
        get => _cpuUsagePercent;
        set { if (Set(ref _cpuUsagePercent, value)) OnPropertyChanged(nameof(PerformanceLabel)); }
    }

    [JsonIgnore]
    public double MemoryMb
    {
        get => _memoryMb;
        set { if (Set(ref _memoryMb, value)) OnPropertyChanged(nameof(PerformanceLabel)); }
    }

    [JsonIgnore]
    public bool CanLaunch => !string.IsNullOrWhiteSpace(LaunchTarget);

    [JsonIgnore]
    public string StatsKey => $"{Launcher}|{(string.IsNullOrWhiteSpace(SourceId) ? InstallPath + "|" + Name : SourceId)}".ToLowerInvariant();

    [JsonIgnore]
    public string StatusLabel => IsRunning ? "PLAYING NOW" : "INSTALLED";

    [JsonIgnore]
    public string PlayTimeLabel => FormatDuration(TotalPlayTimeSeconds);

    [JsonIgnore]
    public string SessionLabel => IsRunning ? $"Session {FormatDuration(CurrentSessionSeconds)}" : $"{LaunchCount} sessions";

    [JsonIgnore]
    public string PerformanceLabel => IsRunning ? $"{CpuUsagePercent:0}% CPU  •  {MemoryMb:0} MB" : "Ready";

    private static string FormatDuration(long seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes}m";
        if (time.TotalMinutes >= 1) return $"{(int)time.TotalMinutes}m";
        return "<1m";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;
}
