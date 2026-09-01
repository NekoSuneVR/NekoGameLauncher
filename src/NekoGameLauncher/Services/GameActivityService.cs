using NekoGameLauncher.Models;
using System.Diagnostics;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class GameActivityService
{
    private readonly Dictionary<string, GameActivityStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveSession> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, CpuSample> _cpuSamples = [];
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private readonly string _statsFile;
    private DateTimeOffset _lastSave = DateTimeOffset.MinValue;

    public GameActivityService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NekoGameLauncher");
        Directory.CreateDirectory(folder);
        _statsFile = Path.Combine(folder, "playtime.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statsFile)) return;
        try
        {
            var json = await File.ReadAllTextAsync(_statsFile, cancellationToken);
            var items = JsonSerializer.Deserialize<List<GameActivityStats>>(json) ?? [];
            _stats.Clear();
            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Key))) _stats[item.Key] = item;
        }
        catch { }
    }

    public async Task<GameActivityUpdate> UpdateAsync(IEnumerable<GameEntry> games, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var processes = ReadProcesses();
        var activeNames = new List<string>();
        var activePids = new HashSet<int>();
        var anyChange = false;

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stats = GetStats(game);
            var matched = MatchProcesses(game, processes);
            var running = matched.Count > 0;

            if (running)
            {
                activeNames.Add(game.Name);
                foreach (var item in matched) activePids.Add(item.Process.Id);

                if (!_active.TryGetValue(game.StatsKey, out var session))
                {
                    session = new ActiveSession { StartedAt = now, LastTick = now };
                    _active[game.StatsKey] = session;
                    stats.LaunchCount++;
                    anyChange = true;
                }

                var elapsed = Math.Clamp((now - session.LastTick).TotalSeconds, 0, 15);
                if (elapsed >= 1)
                {
                    stats.TotalPlayTimeSeconds += (long)Math.Round(elapsed);
                    session.LastTick = now;
                    anyChange = true;
                }
                stats.LastPlayedAt = now;

                var cpu = 0d;
                var memory = 0d;
                foreach (var item in matched)
                {
                    cpu += ReadProcessCpu(item.Process, now);
                    try { memory += item.Process.WorkingSet64 / 1024d / 1024d; } catch { }
                }

                game.IsRunning = true;
                game.TotalPlayTimeSeconds = stats.TotalPlayTimeSeconds;
                game.LaunchCount = stats.LaunchCount;
                game.LastPlayedAt = stats.LastPlayedAt;
                game.CurrentSessionSeconds = (long)Math.Max(0, (now - session.StartedAt).TotalSeconds);
                game.CpuUsagePercent = Math.Clamp(cpu, 0, 100);
                game.MemoryMb = memory;
            }
            else
            {
                if (_active.Remove(game.StatsKey)) anyChange = true;
                game.IsRunning = false;
                game.TotalPlayTimeSeconds = stats.TotalPlayTimeSeconds;
                game.LaunchCount = stats.LaunchCount;
                game.LastPlayedAt = stats.LastPlayedAt;
                game.CurrentSessionSeconds = 0;
                game.CpuUsagePercent = 0;
                game.MemoryMb = 0;
            }
        }

        foreach (var item in processes) item.Process.Dispose();
        CleanupCpuSamples(activePids);

        if (anyChange && now - _lastSave > TimeSpan.FromSeconds(20))
        {
            await SaveAsync(cancellationToken);
            _lastSave = now;
        }

        return new GameActivityUpdate
        {
            ActiveGameCount = activeNames.Count,
            ActiveGameName = activeNames.FirstOrDefault() ?? string.Empty,
            GameProcessIds = activePids.ToList()
        };
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = _stats.Values.OrderByDescending(x => x.TotalPlayTimeSeconds).ToList();
            await File.WriteAllTextAsync(_statsFile, JsonSerializer.Serialize(data, _json), cancellationToken);
        }
        catch { }
    }

    private GameActivityStats GetStats(GameEntry game)
    {
        if (_stats.TryGetValue(game.StatsKey, out var existing)) return existing;
        var created = new GameActivityStats
        {
            Key = game.StatsKey,
            TotalPlayTimeSeconds = game.TotalPlayTimeSeconds,
            LaunchCount = game.LaunchCount,
            LastPlayedAt = game.LastPlayedAt
        };
        _stats[game.StatsKey] = created;
        return created;
    }

    private static List<ProcessInfo> ReadProcesses()
    {
        var result = new List<ProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path)) result.Add(new ProcessInfo(process, Normalize(path)));
                else process.Dispose();
            }
            catch { process.Dispose(); }
        }
        return result;
    }

    private static List<ProcessInfo> MatchProcesses(GameEntry game, IReadOnlyList<ProcessInfo> processes)
    {
        var install = Normalize(game.InstallPath);
        var launch = File.Exists(game.LaunchTarget) ? Normalize(game.LaunchTarget) : string.Empty;
        if (string.IsNullOrWhiteSpace(install) && string.IsNullOrWhiteSpace(launch)) return [];

        return processes.Where(item =>
        {
            if (!string.IsNullOrWhiteSpace(launch) && item.Path.Equals(launch, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(install)) return false;
            var root = install.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return item.Path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !LooksLikeInstaller(item.Path);
        }).ToList();
    }

    private double ReadProcessCpu(Process process, DateTimeOffset now)
    {
        try
        {
            var currentCpu = process.TotalProcessorTime;
            if (!_cpuSamples.TryGetValue(process.Id, out var previous))
            {
                _cpuSamples[process.Id] = new CpuSample(now, currentCpu);
                return 0;
            }
            _cpuSamples[process.Id] = new CpuSample(now, currentCpu);
            var wall = (now - previous.At).TotalMilliseconds;
            if (wall <= 0) return 0;
            var cpuMs = (currentCpu - previous.Cpu).TotalMilliseconds;
            return Math.Max(0, cpuMs / wall / Math.Max(1, Environment.ProcessorCount) * 100d);
        }
        catch { return 0; }
    }

    private void CleanupCpuSamples(HashSet<int> activePids)
    {
        foreach (var pid in _cpuSamples.Keys.Where(pid => !activePids.Contains(pid)).ToArray()) _cpuSamples.Remove(pid);
    }

    private static bool LooksLikeInstaller(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return name.Contains("unins") || name.Contains("uninstall") || name.Contains("setup") || name.Contains("crashreport") || name.Contains("reporter");
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path.Trim().Trim('"')); }
        catch { return path.Trim().Trim('"'); }
    }

    private sealed class ActiveSession
    {
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset LastTick { get; set; }
    }

    private sealed record ProcessInfo(Process Process, string Path);
    private sealed record CpuSample(DateTimeOffset At, TimeSpan Cpu);
}
