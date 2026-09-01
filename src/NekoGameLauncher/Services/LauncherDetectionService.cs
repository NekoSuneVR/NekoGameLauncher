using NekoGameLauncher.Models;

namespace NekoGameLauncher.Services;

public sealed class LauncherDetectionService
{
    public IReadOnlyList<LauncherStatus> Detect(IEnumerable<GameEntry> games)
    {
        var gameList = games.ToList();
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Steam"] = [SteamGameProvider.FindSteamPath() is { Length: > 0 } sp ? Path.Combine(sp, "steam.exe") : string.Empty],
            ["Epic Games"] = [Path.Combine(pfx86, "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe")],
            ["EA / Origin"] = [Path.Combine(pf, "Electronic Arts", "EA Desktop", "EA Desktop", "EADesktop.exe"), Path.Combine(pfx86, "Origin", "Origin.exe")],
            ["Ubisoft Connect"] = [Path.Combine(pfx86, "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe")],
            ["GOG Galaxy"] = [Path.Combine(pfx86, "GOG Galaxy", "GalaxyClient.exe")],
            ["Battle.net"] = [Path.Combine(pfx86, "Battle.net", "Battle.net Launcher.exe")],
            ["Rockstar Games"] = [Path.Combine(pf, "Rockstar Games", "Launcher", "Launcher.exe")],
            ["Riot Games"] = [Path.Combine(common, "Riot Games", "RiotClientInstalls.json"), Path.Combine(local, "Riot Games", "Riot Client", "RiotClientServices.exe")],
            ["Xbox / Microsoft Store"] = [Path.Combine(local, "Microsoft", "WindowsApps", "XboxPcApp.exe")],
            ["Wargaming.net"] = [Path.Combine(common, "Wargaming.net", "GameCenter", "wgc.exe"), Path.Combine(pfx86, "Wargaming.net", "GameCenter", "wgc.exe")],
            ["HoYoPlay"] = [Path.Combine(pf, "HoYoPlay", "launcher.exe"), Path.Combine(pfx86, "HoYoPlay", "launcher.exe")],
            ["Kuro Games"] = [Path.Combine(pf, "Wuthering Waves", "launcher.exe"), Path.Combine(pfx86, "Wuthering Waves", "launcher.exe")]
        };

        var result = candidates.Select(item =>
        {
            var path = item.Value.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)) ?? string.Empty;
            return new LauncherStatus
            {
                Name = item.Key,
                IsInstalled = !string.IsNullOrWhiteSpace(path) || gameList.Any(g => g.Launcher.Equals(item.Key, StringComparison.OrdinalIgnoreCase)),
                ExecutablePath = path,
                GameCount = gameList.Count(g => g.Launcher.Equals(item.Key, StringComparison.OrdinalIgnoreCase))
            };
        }).ToList();

        foreach (var group in gameList.GroupBy(g => g.Launcher, StringComparer.OrdinalIgnoreCase))
        {
            if (result.Any(x => x.Name.Equals(group.Key, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(new LauncherStatus { Name = group.Key, IsInstalled = true, GameCount = group.Count() });
        }
        return result.OrderByDescending(x => x.IsInstalled).ThenBy(x => x.Name).ToList();
    }
}
