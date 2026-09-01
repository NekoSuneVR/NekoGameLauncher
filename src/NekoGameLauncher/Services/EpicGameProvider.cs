using NekoGameLauncher.Models;
using System.Diagnostics;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class EpicGameProvider : IGameLibraryProvider
{
    public string Name => "Epic Games";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameEntry>();

        // 1) Standard Epic .item manifests. These contain the best display names and launch identities.
        ScanManifestItems(games, cancellationToken);

        // 2) Epic's launcher-wide installation inventory. Some current installs are present here even when
        // the .item manifest directory is empty/moved, which is why games such as GTA V Enhanced can be missed.
        ScanLauncherInstalledDatabase(games, cancellationToken);

        // 3) Last-resort local discovery. Epic installs normally contain an .egstore directory.
        ScanEpicInstallFolders(games, cancellationToken);

        return Task.FromResult<IReadOnlyList<GameEntry>>(games
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private void ScanManifestItems(List<GameEntry> games, CancellationToken cancellationToken)
    {
        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var roots = new[]
        {
            Path.Combine(commonData, "Epic", "EpicGamesLauncher", "Data", "Manifests"),
            Path.Combine(commonData, "Epic", "EpicGamesLauncher", "Data", "EMS", "current")
        };

        foreach (var manifestRoot in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(manifestRoot)) continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(manifestRoot, "*.item", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    var root = doc.RootElement;
                    var install = GetAny(root, "InstallLocation", "InstallPath");
                    if (!string.IsNullOrWhiteSpace(install) && !Directory.Exists(install)) continue;

                    var appName = GetAny(root, "AppName", "ArtifactId");
                    var catalog = GetAny(root, "CatalogItemId", "ItemId");
                    var ns = GetAny(root, "CatalogNamespace", "NamespaceId");
                    var executable = GetAny(root, "LaunchExecutable", "Executable");
                    var args = GetAny(root, "LaunchCommand", "LaunchArguments");
                    var exePath = ResolveExecutable(install, executable);
                    var title = GetAny(root, "DisplayName", "Title");
                    if (string.IsNullOrWhiteSpace(title))
                        title = GetFriendlyName(install, exePath, appName);
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var uri = BuildEpicUri(ns, catalog, appName);
                    AddUnique(games, new GameEntry
                    {
                        Name = title,
                        Launcher = Name,
                        SourceId = appName,
                        InstallPath = install,
                        LaunchTarget = !string.IsNullOrWhiteSpace(uri) ? uri : exePath,
                        LaunchArguments = args,
                        IsInstalled = string.IsNullOrWhiteSpace(install) || Directory.Exists(install)
                    });
                }
                catch { }
            }
        }
    }

    private void ScanLauncherInstalledDatabase(List<GameEntry> games, CancellationToken cancellationToken)
    {
        var database = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");

        if (!File.Exists(database)) return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(database));
            if (!TryGetPropertyIgnoreCase(doc.RootElement, "InstallationList", out var list) || list.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in list.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.ValueKind != JsonValueKind.Object) continue;

                var install = GetAny(item, "InstallLocation", "InstallPath");
                if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) continue;

                var appName = GetAny(item, "AppName", "ArtifactId");
                var artifact = GetAny(item, "ArtifactId", "AppName");
                var ns = GetAny(item, "NamespaceId", "CatalogNamespace");
                var itemId = GetAny(item, "ItemId", "CatalogItemId");
                var exePath = FindLikelyExecutable(install);
                var title = GetAny(item, "DisplayName", "Title");
                if (string.IsNullOrWhiteSpace(title))
                    title = GetFriendlyName(install, exePath, artifact);

                var uri = BuildEpicUri(ns, itemId, artifact);
                if (string.IsNullOrWhiteSpace(uri) && !string.IsNullOrWhiteSpace(appName))
                    uri = $"com.epicgames.launcher://apps/{Uri.EscapeDataString(appName)}?action=launch&silent=true";

                AddUnique(games, new GameEntry
                {
                    Name = title,
                    Launcher = Name,
                    SourceId = appName,
                    InstallPath = install,
                    LaunchTarget = !string.IsNullOrWhiteSpace(uri) ? uri : exePath,
                    IsInstalled = true
                });
            }
        }
        catch { }
    }

    private void ScanEpicInstallFolders(List<GameEntry> games, CancellationToken cancellationToken)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                roots.Add(Path.Combine(drive.RootDirectory.FullName, "Epic Games"));
                roots.Add(Path.Combine(drive.RootDirectory.FullName, "EpicGames"));
            }
        }
        catch { }

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) continue;

            IEnumerable<string> directories;
            try { directories = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly); }
            catch { continue; }

            foreach (var install in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!Directory.Exists(Path.Combine(install, ".egstore"))) continue;
                    if (Path.GetFileName(install).Equals("Launcher", StringComparison.OrdinalIgnoreCase)) continue;

                    var exePath = FindLikelyExecutable(install);
                    var title = GetFriendlyName(install, exePath, Path.GetFileName(install));
                    AddUnique(games, new GameEntry
                    {
                        Name = title,
                        Launcher = Name,
                        SourceId = install,
                        InstallPath = install,
                        LaunchTarget = exePath,
                        IsInstalled = true
                    });
                }
                catch { }
            }
        }
    }

    private static void AddRoot(HashSet<string> roots, string programFiles)
    {
        if (string.IsNullOrWhiteSpace(programFiles)) return;
        roots.Add(Path.Combine(programFiles, "Epic Games"));
        roots.Add(Path.Combine(programFiles, "EpicGames"));
    }

    private static string ResolveExecutable(string install, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return FindLikelyExecutable(install);
        if (Path.IsPathRooted(executable) && File.Exists(executable)) return executable;
        if (!string.IsNullOrWhiteSpace(install))
        {
            var combined = Path.Combine(install, executable.TrimStart('\\', '/'));
            if (File.Exists(combined)) return combined;
        }
        return FindLikelyExecutable(install);
    }

    private static string FindLikelyExecutable(string install)
    {
        if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install)) return string.Empty;

        var candidates = new List<string>();
        TryCollectExecutables(install, candidates);

        foreach (var relative in new[] { "Binaries\\Win64", "Binaries\\Win32", "bin", "Game" })
        {
            var folder = Path.Combine(install, relative);
            if (Directory.Exists(folder)) TryCollectExecutables(folder, candidates);
        }

        return candidates
            .Where(path => !IsUtilityExecutable(Path.GetFileNameWithoutExtension(path)))
            .OrderByDescending(GetSafeFileSize)
            .FirstOrDefault()
            ?? candidates.OrderByDescending(GetSafeFileSize).FirstOrDefault()
            ?? string.Empty;
    }

    private static void TryCollectExecutables(string folder, List<string> candidates)
    {
        try { candidates.AddRange(Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)); }
        catch { }
    }

    private static bool IsUtilityExecutable(string name)
    {
        var value = name.ToLowerInvariant();
        return value.Contains("unins") || value.Contains("uninstall") || value.Contains("setup") ||
               value.Contains("installer") || value.Contains("crash") || value.Contains("report") ||
               value.Contains("helper") || value.Contains("updater") || value.Contains("redist") ||
               value.Contains("easyanticheat") || value.Contains("epicwebhelper");
    }

    private static long GetSafeFileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static string GetFriendlyName(string install, string executable, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(executable);
                if (!string.IsNullOrWhiteSpace(info.ProductName) &&
                    !info.ProductName.Contains("launcher", StringComparison.OrdinalIgnoreCase))
                    return info.ProductName.Trim();
            }
            catch { }
        }

        var raw = !string.IsNullOrWhiteSpace(install) ? Path.GetFileName(install.TrimEnd('\\', '/')) : fallback;
        if (string.IsNullOrWhiteSpace(raw)) raw = fallback;
        raw = raw.Replace('_', ' ').Replace('-', ' ');

        var human = new System.Text.StringBuilder();
        for (var i = 0; i < raw.Length; i++)
        {
            var current = raw[i];
            if (i > 0)
            {
                var previous = raw[i - 1];
                var next = i + 1 < raw.Length ? raw[i + 1] : '\0';
                if ((char.IsUpper(current) && char.IsLower(previous)) ||
                    (char.IsUpper(current) && char.IsUpper(previous) && char.IsLower(next)) ||
                    (char.IsDigit(current) && !char.IsDigit(previous)) ||
                    (!char.IsDigit(current) && char.IsDigit(previous)))
                    human.Append(' ');
            }
            human.Append(current);
        }

        var result = string.Join(' ', human.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (result.StartsWith("GTAV ", StringComparison.OrdinalIgnoreCase))
            result = "GTA V " + result[5..];
        return result;
    }

    private static string BuildEpicUri(string ns, string catalog, string appName)
    {
        if (string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(catalog) || string.IsNullOrWhiteSpace(appName))
            return string.Empty;

        var identity = Uri.EscapeDataString($"{ns}:{catalog}:{appName}");
        return $"com.epicgames.launcher://apps/{identity}?action=launch&silent=true";
    }

    private static void AddUnique(List<GameEntry> games, GameEntry game)
    {
        if (string.IsNullOrWhiteSpace(game.Name)) return;

        var duplicate = games.Any(existing =>
            (!string.IsNullOrWhiteSpace(game.InstallPath) && !string.IsNullOrWhiteSpace(existing.InstallPath) &&
             string.Equals(NormalizePath(game.InstallPath), NormalizePath(existing.InstallPath), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(game.SourceId) && !string.IsNullOrWhiteSpace(existing.SourceId) &&
             string.Equals(game.SourceId, existing.SourceId, StringComparison.OrdinalIgnoreCase)));

        if (!duplicate) games.Add(game);
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\', '/'); }
        catch { return path.TrimEnd('\\', '/'); }
    }

    private static string GetAny(JsonElement element, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (!TryGetPropertyIgnoreCase(element, property, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return value.GetRawText();
        }
        return string.Empty;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string property, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(property, out value)) return true;
            foreach (var candidate in element.EnumerateObject())
            {
                if (!candidate.Name.Equals(property, StringComparison.OrdinalIgnoreCase)) continue;
                value = candidate.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
