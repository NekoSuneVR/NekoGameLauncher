using Microsoft.Win32;
using NekoGameLauncher.Models;

namespace NekoGameLauncher.Services;

public sealed class KnownGameProvider : IGameLibraryProvider
{
    public string Name => "PC Game Discovery";

    private static readonly Definition[] Definitions =
    [
        new("World of Tanks", "Wargaming.net", ["World of Tanks"], ["WorldOfTanks.exe"], ["Games\\World_of_Tanks_EU", "Games\\World_of_Tanks_NA", "Games\\World_of_Tanks_ASIA"]),
        new("World of Warships", "Wargaming.net", ["World of Warships"], ["WorldOfWarships.exe"], ["Games\\World_of_Warships_EU", "Games\\World_of_Warships_NA", "Games\\World_of_Warships_ASIA"]),
        new("World of Warplanes", "Wargaming.net", ["World of Warplanes"], ["WorldOfWarplanes.exe"], ["Games\\World_of_Warplanes_EU", "Games\\World_of_Warplanes_NA"]),
        new("Zenless Zone Zero", "HoYoPlay", ["Zenless Zone Zero", "ZenlessZoneZero"], ["ZenlessZoneZero.exe"], ["Program Files\\HoYoPlay\\games\\ZenlessZoneZero Game", "Games\\Zenless Zone Zero"]),
        new("Genshin Impact", "HoYoPlay", ["Genshin Impact"], ["GenshinImpact.exe"], ["Program Files\\HoYoPlay\\games\\Genshin Impact game", "Games\\Genshin Impact"]),
        new("Honkai: Star Rail", "HoYoPlay", ["Honkai: Star Rail", "Star Rail"], ["StarRail.exe"], ["Program Files\\HoYoPlay\\games\\Star Rail Games", "Games\\Star Rail"]),
        new("Honkai Impact 3rd", "HoYoPlay", ["Honkai Impact 3rd", "Honkai Impact 3"], ["BH3.exe", "HonkaiImpact3.exe"], ["Program Files\\HoYoPlay\\games\\Honkai Impact 3rd game"]),
        new("Wuthering Waves", "Kuro Games", ["Wuthering Waves", "WutheringWaves"], ["Wuthering Waves.exe", "Client-Win64-Shipping.exe"], ["Wuthering Waves", "Games\\Wuthering Waves", "Program Files\\Wuthering Waves", "Program Files\\Kuro Games\\Wuthering Waves"]),
        new("Punishing: Gray Raven", "Kuro Games", ["Punishing Gray Raven", "Punishing: Gray Raven"], ["PGR.exe", "PunishingGrayRaven.exe"], ["Games\\Punishing Gray Raven", "Program Files\\Kuro Games\\Punishing Gray Raven"]),
        new("Neverness to Everness", "Perfect World / NTE", ["Neverness to Everness", "NTE"], ["NTE.exe", "NevernessToEverness.exe"], ["Games\\NTE", "Games\\Neverness to Everness", "Program Files\\NTE", "Program Files\\Neverness to Everness"]),
        new("Infinity Nikki", "Infold Games", ["Infinity Nikki"], ["X6Game-Win64-Shipping.exe", "InfinityNikki.exe"], ["Games\\Infinity Nikki", "Program Files\\Infinity Nikki"]),
        new("Tower of Fantasy", "Perfect World", ["Tower of Fantasy"], ["QRSL.exe", "TowerOfFantasy.exe"], ["Games\\Tower of Fantasy", "Program Files\\Tower of Fantasy"]),
        new("Snowbreak: Containment Zone", "Seasun Games", ["Snowbreak", "Containment Zone"], ["Snowbreak.exe"], ["Games\\Snowbreak", "Program Files\\Snowbreak"]),
        new("Once Human", "NetEase Games", ["Once Human"], ["ONCE_HUMAN.exe", "OnceHuman.exe"], ["Games\\Once Human", "Program Files\\Once Human"]),
        new("Star Citizen", "RSI Launcher", ["Star Citizen"], ["StarCitizen.exe"], ["Program Files\\Roberts Space Industries\\StarCitizen", "Games\\StarCitizen"]),
        new("Escape from Tarkov", "Battlestate Games", ["Escape from Tarkov"], ["EscapeFromTarkov.exe"], ["Battlestate Games\\EFT", "Games\\Escape from Tarkov"]),
        new("Warframe", "Standalone", ["Warframe"], ["Warframe.x64.exe"], ["Games\\Warframe"]),
        new("FINAL FANTASY XIV", "Standalone", ["FINAL FANTASY XIV", "FFXIV"], ["ffxiv_dx11.exe", "ffxiv.exe"], ["Games\\FINAL FANTASY XIV - A Realm Reborn", "Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn"]),
        new("Guild Wars 2", "Standalone", ["Guild Wars 2"], ["Gw2-64.exe"], ["Games\\Guild Wars 2", "Program Files\\Guild Wars 2"]),
        new("Path of Exile", "Standalone", ["Path of Exile"], ["PathOfExile_x64.exe", "PathOfExile.exe"], ["Games\\Path of Exile", "Program Files (x86)\\Grinding Gear Games\\Path of Exile"]),
        new("osu!", "Standalone", ["osu!"], ["osu!.exe"], ["Games\\osu!"])
    ];

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<GameEntry>();
        var uninstall = ReadUninstallEntries();

        foreach (var definition in Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var item in uninstall.Where(x => definition.Keywords.Any(k => x.DisplayName.Contains(k, StringComparison.OrdinalIgnoreCase))))
                Add(results, definition, item.InstallLocation, item.DisplayName);

            foreach (var folder in FindCommonFolders(definition)) Add(results, definition, folder, definition.Name);
        }

        var unique = results
            .Where(x => !string.IsNullOrWhiteSpace(x.InstallPath) && Directory.Exists(x.InstallPath))
            .GroupBy(x => $"{x.Launcher}|{x.InstallPath}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        return Task.FromResult<IReadOnlyList<GameEntry>>(unique);
    }

    private static void Add(List<GameEntry> results, Definition definition, string? installPath, string displayName)
    {
        var root = NormalizeDirectory(installPath);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
        var executable = FindExecutable(root, definition.Executables);
        results.Add(new GameEntry
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? definition.Name : CleanDisplayName(displayName, definition.Name),
            Launcher = definition.Launcher,
            SourceId = $"{definition.Name}|{root}".ToLowerInvariant(),
            InstallPath = root,
            LaunchTarget = executable,
            IsInstalled = true
        });
    }

    private static IEnumerable<string> FindCommonFolders(Definition definition)
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToArray(); }
        catch { yield break; }

        foreach (var drive in drives)
        foreach (var relative in definition.RelativeFolders)
        {
            var candidate = Path.Combine(drive.RootDirectory.FullName, relative);
            if (Directory.Exists(candidate)) yield return candidate;
        }
    }

    private static string FindExecutable(string root, IReadOnlyList<string> names)
    {
        var wanted = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Folder, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (folder, depth) = queue.Dequeue();
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
                    if (wanted.Contains(Path.GetFileName(file))) return file;

                if (depth >= 4) continue;
                foreach (var child in Directory.EnumerateDirectories(folder))
                {
                    var name = Path.GetFileName(child).ToLowerInvariant();
                    if (name is "redist" or "redistributables" or "logs" or "cache" or "temp" || name.Contains("uninstall")) continue;
                    queue.Enqueue((child, depth + 1));
                }
            }
            catch { }
        }
        return string.Empty;
    }

    private static List<UninstallEntry> ReadUninstallEntries()
    {
        var result = new List<UninstallEntry>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var keyName in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(keyName);
                    var displayName = key?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;
                    var install = key?.GetValue("InstallLocation") as string;
                    if (string.IsNullOrWhiteSpace(install)) install = InferFolder(key?.GetValue("DisplayIcon") as string);
                    if (string.IsNullOrWhiteSpace(install)) install = InferFolder(key?.GetValue("UninstallString") as string);
                    result.Add(new UninstallEntry(displayName, install ?? string.Empty));
                }
            }
            catch { }
        }
        return result;
    }

    private static string InferFolder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Trim();
        if (cleaned.StartsWith('"'))
        {
            var end = cleaned.IndexOf('"', 1);
            if (end > 1) cleaned = cleaned[1..end];
        }
        else
        {
            var comma = cleaned.IndexOf(',');
            if (comma > 0) cleaned = cleaned[..comma];
        }
        try
        {
            if (File.Exists(cleaned)) return Path.GetDirectoryName(cleaned) ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static string NormalizeDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'))); }
        catch { return value.Trim().Trim('"'); }
    }

    private static string CleanDisplayName(string displayName, string fallback)
    {
        var name = displayName.Trim();
        foreach (var suffix in new[] { " Launcher", " Game Launcher", " Uninstaller" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) name = name[..^suffix.Length].Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private sealed record Definition(string Name, string Launcher, string[] Keywords, string[] Executables, string[] RelativeFolders);
    private sealed record UninstallEntry(string DisplayName, string InstallLocation);
}
