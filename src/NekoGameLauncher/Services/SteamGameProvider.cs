using Microsoft.Win32;
using NekoGameLauncher.Models;
using System.Text.RegularExpressions;

namespace NekoGameLauncher.Services;

public sealed class SteamGameProvider : IGameLibraryProvider
{
    public string Name => "Steam";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameEntry>();
        var steamPath = FindSteamPath();
        if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
            return Task.FromResult<IReadOnlyList<GameEntry>>(games);

        foreach (var library in FindLibraries(steamPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var data = ParseKeyValues(File.ReadAllText(manifest));
                    if (!data.TryGetValue("appid", out var appId) || !data.TryGetValue("name", out var name)) continue;
                    data.TryGetValue("installdir", out var installDir);
                    var installPath = string.IsNullOrWhiteSpace(installDir) ? string.Empty : Path.Combine(steamApps, "common", installDir);
                    games.Add(new GameEntry
                    {
                        Name = name,
                        Launcher = Name,
                        SourceId = appId,
                        InstallPath = installPath,
                        LaunchTarget = $"steam://rungameid/{appId}",
                        IsInstalled = string.IsNullOrWhiteSpace(installPath) || Directory.Exists(installPath)
                    });
                }
                catch { }
            }
        }

        return Task.FromResult<IReadOnlyList<GameEntry>>(games);
    }

    public static string? FindSteamPath()
    {
        foreach (var keyPath in new[] { @"Software\Valve\Steam", @"Software\WOW6432Node\Valve\Steam" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath) ?? Registry.LocalMachine.OpenSubKey(keyPath);
            var path = key?.GetValue("SteamPath")?.ToString() ?? key?.GetValue("InstallPath")?.ToString();
            if (!string.IsNullOrWhiteSpace(path)) return path.Replace('/', '\\');
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static IEnumerable<string> FindLibraries(string steamPath)
    {
        yield return steamPath;
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        string text;
        try { text = File.ReadAllText(vdf); } catch { yield break; }
        foreach (Match match in VdfPathRegex.Matches(text))
        {
            var path = match.Groups[1].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }
    }

    private static Dictionary<string, string> ParseKeyValues(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeyValueRegex.Matches(input))
            result[match.Groups[1].Value] = match.Groups[2].Value;
        return result;
    }

    private static readonly Regex VdfPathRegex = new(@"""path""\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"""([^""]+)""\s*""([^""]*)""", RegexOptions.Compiled);
}
