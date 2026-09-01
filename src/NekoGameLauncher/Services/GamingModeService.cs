using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NekoGameLauncher.Services;

public sealed class GamingModeService
{
    private readonly Dictionary<int, ProcessPriorityClass> _originalPriorities = [];
    private string _previousPowerScheme = string.Empty;

    public bool IsEnabled { get; private set; }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken = default)
    {
        if (IsEnabled) return true;
        try
        {
            var active = await RunAsync("powercfg.exe", "/getactivescheme", cancellationToken);
            _previousPowerScheme = Regex.Match(active, "[0-9a-fA-F-]{36}").Value;
            await RunAsync("powercfg.exe", "/setactive SCHEME_MIN", cancellationToken);
            IsEnabled = true;
            return true;
        }
        catch
        {
            IsEnabled = false;
            return false;
        }
    }

    public void BoostProcesses(IEnumerable<int> processIds, bool enabled)
    {
        if (!IsEnabled || !enabled) return;
        foreach (var pid in processIds.Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!_originalPriorities.ContainsKey(pid)) _originalPriorities[pid] = process.PriorityClass;
                if (process.PriorityClass is ProcessPriorityClass.Normal or ProcessPriorityClass.BelowNormal or ProcessPriorityClass.Idle)
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
            }
            catch { }
        }
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in _originalPriorities.ToArray())
        {
            try
            {
                using var process = Process.GetProcessById(item.Key);
                process.PriorityClass = item.Value;
            }
            catch { }
        }
        _originalPriorities.Clear();

        if (IsEnabled && !string.IsNullOrWhiteSpace(_previousPowerScheme))
        {
            try { await RunAsync("powercfg.exe", $"/setactive {_previousPowerScheme}", cancellationToken); }
            catch { }
        }
        IsEnabled = false;
        _previousPowerScheme = string.Empty;
    }

    public static void OpenWindowsGameModeSettings() => Open("ms-settings:gaming-gamemode");
    public static void OpenGraphicsSettings() => Open("ms-settings:display-advancedgraphics");
    public static void OpenTaskManager() => Open("taskmgr.exe");

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { }
    }

    private static async Task<string> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException($"Could not start {fileName}");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(await process.StandardError.ReadToEndAsync());
        return output;
    }
}
