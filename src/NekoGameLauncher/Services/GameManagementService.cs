using Microsoft.Win32;
using NekoGameLauncher.Models;
using System.Diagnostics;

namespace NekoGameLauncher.Services;

public sealed class GameManagementService
{
    public UninstallPlan GetUninstallPlan(GameEntry game)
    {
        var registered = FindRegisteredUninstaller(game);
        if (registered is not null)
            return new UninstallPlan("Windows registered uninstaller", registered.UninstallString, UninstallPlanKind.Command,
                $"Uses the uninstall command registered by {registered.DisplayName}.");

        if (game.Launcher.Equals("Steam", StringComparison.OrdinalIgnoreCase) && game.SourceId.All(char.IsDigit) && !string.IsNullOrWhiteSpace(game.SourceId))
            return new UninstallPlan("Steam uninstall", $"steam://uninstall/{game.SourceId}", UninstallPlanKind.Uri,
                "Steam will open its normal uninstall confirmation for this game.");

        if (game.Launcher.Equals("Epic Games", StringComparison.OrdinalIgnoreCase))
            return new UninstallPlan("Open Epic Games Library", "com.epicgames.launcher://library", UninstallPlanKind.Uri,
                "Epic does not expose a dependable public per-game uninstall URI, so Neko opens the Epic library for the normal uninstall flow.");

        return new UninstallPlan("Windows Apps & Features", "ms-settings:appsfeatures", UninstallPlanKind.Uri,
            "No safe direct uninstaller was found, so Windows Apps & Features will open instead.");
    }

    public bool StartUninstall(GameEntry game, out string message)
    {
        if (game.IsRunning)
        {
            message = "Close the game before uninstalling it.";
            return false;
        }

        var plan = GetUninstallPlan(game);
        try
        {
            if (plan.Kind == UninstallPlanKind.Command)
                StartCommand(plan.Target);
            else
                Process.Start(new ProcessStartInfo(plan.Target) { UseShellExecute = true });
            message = $"Started: {plan.Label}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Could not start the uninstaller: {ex.Message}";
            return false;
        }
    }

    public bool OpenInstallFolder(GameEntry game)
    {
        if (string.IsNullOrWhiteSpace(game.InstallPath) || !Directory.Exists(game.InstallPath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{game.InstallPath}\"") { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    public static void OpenAppsFeatures()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); }
        catch { }
    }

    public IReadOnlyList<CleanupCandidate> GetCleanupCandidates(GameEntry game, bool includeUserData)
    {
        var candidates = new List<CleanupCandidate>();
        if (!string.IsNullOrWhiteSpace(game.InstallPath) && Directory.Exists(game.InstallPath))
        {
            var safe = IsGameScopedInstallPath(game.InstallPath, game.Name);
            candidates.Add(new CleanupCandidate(game.InstallPath, safe ? "Install remnants" : "Install path - manual review", false, safe, GetDirectorySize(game.InstallPath)));
        }

        AddMatchingTempFolders(candidates, game.Name);
        if (includeUserData) AddUserDataFolders(candidates, game.Name);

        return candidates
            .Where(candidate => Directory.Exists(candidate.Path))
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.UserData)
            .ThenBy(candidate => candidate.Category)
            .ThenBy(candidate => candidate.Path)
            .ToList();
    }

    public async Task<CleanupResult> DeleteCandidatesAsync(IEnumerable<CleanupCandidate> candidates, bool includeUserData, CancellationToken cancellationToken = default)
    {
        var deleted = new List<string>();
        var failed = new List<string>();

        foreach (var candidate in candidates.Where(candidate => candidate.SafeToDelete && (!candidate.UserData || includeUserData)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSafeCleanupPath(candidate.Path, candidate.UserData))
            {
                failed.Add($"{candidate.Path} (safety check blocked it)");
                continue;
            }

            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(candidate.Path)) Directory.Delete(candidate.Path, true);
                }, cancellationToken);
                deleted.Add(candidate.Path);
            }
            catch (Exception ex)
            {
                failed.Add($"{candidate.Path} ({ex.Message})");
            }
        }

        return new CleanupResult(deleted, failed);
    }

    private static RegisteredUninstaller? FindRegisteredUninstaller(GameEntry game)
    {
        RegisteredUninstaller? best = null;
        var bestScore = 0;
        foreach (var entry in EnumerateRegisteredUninstallers())
        {
            if (string.IsNullOrWhiteSpace(entry.UninstallString)) continue;
            var score = 0;
            if (!string.IsNullOrWhiteSpace(game.InstallPath) && !string.IsNullOrWhiteSpace(entry.InstallLocation))
            {
                var gamePath = NormalizePath(game.InstallPath);
                var entryPath = NormalizePath(entry.InstallLocation);
                if (gamePath.Equals(entryPath, StringComparison.OrdinalIgnoreCase)) score += 120;
                else if (gamePath.StartsWith(entryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                      || entryPath.StartsWith(gamePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) score += 70;
            }

            if (entry.DisplayName.Equals(game.Name, StringComparison.OrdinalIgnoreCase)) score += 100;
            else if (NormalizeName(entry.DisplayName).Equals(NormalizeName(game.Name), StringComparison.OrdinalIgnoreCase)) score += 90;
            else if (NamesLikelyMatch(entry.DisplayName, game.Name)) score += 45;

            if (score > bestScore)
            {
                best = entry;
                bestScore = score;
            }
        }
        return bestScore >= 70 ? best : null;
    }

    private static IEnumerable<RegisteredUninstaller> EnumerateRegisteredUninstallers()
    {
        var entries = new List<RegisteredUninstaller>();
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
                    try
                    {
                        using var key = uninstall.OpenSubKey(keyName);
                        var displayName = key?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;
                        entries.Add(new RegisteredUninstaller(
                            displayName,
                            key?.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                            key?.GetValue("UninstallString")?.ToString() ?? string.Empty));
                    }
                    catch { }
                }
            }
            catch { }
        }
        return entries;
    }

    private static void StartCommand(string command)
    {
        var (fileName, arguments) = SplitCommand(command);
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidOperationException("The registered uninstall command is invalid.");
        Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = File.Exists(fileName) ? Path.GetDirectoryName(fileName) ?? string.Empty : string.Empty
        });
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        var value = command.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            if (end > 1) return (value[1..end], value[(end + 1)..].Trim());
        }

        var exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            var end = exe + 4;
            return (value[..end].Trim().Trim('"'), value[end..].Trim());
        }

        var firstSpace = value.IndexOf(' ');
        return firstSpace > 0 ? (value[..firstSpace], value[(firstSpace + 1)..]) : (value, string.Empty);
    }

    private static void AddMatchingTempFolders(List<CleanupCandidate> candidates, string gameName)
    {
        var tempRoot = Path.GetTempPath();
        if (!Directory.Exists(tempRoot)) return;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(tempRoot, "*", SearchOption.TopDirectoryOnly))
            {
                if (!NamesLikelyMatch(Path.GetFileName(dir), gameName)) continue;
                candidates.Add(new CleanupCandidate(dir, "Temporary game files", false, true, GetDirectorySize(dir)));
            }
        }
        catch { }
    }

    private static void AddUserDataFolders(List<CleanupCandidate> candidates, string gameName)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games")
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!NamesLikelyMatch(Path.GetFileName(dir), gameName)) continue;
                    candidates.Add(new CleanupCandidate(dir, "User settings / saves", true, true, GetDirectorySize(dir)));
                }
            }
            catch { }
        }
    }

    private static bool IsGameScopedInstallPath(string path, string gameName)
    {
        if (!IsSafeCleanupPath(path, false)) return false;
        var folderName = Path.GetFileName(NormalizePath(path));
        return NamesLikelyMatch(folderName, gameName);
    }

    private static bool IsSafeCleanupPath(string path, bool userData)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full;
        try { full = NormalizePath(path); }
        catch { return false; }
        if (full.Length < 8 || Path.GetPathRoot(full)?.Equals(full, StringComparison.OrdinalIgnoreCase) == true) return false;

        var protectedRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(NormalizePath).ToArray();

        if (protectedRoots.Any(root => full.Equals(root, StringComparison.OrdinalIgnoreCase))) return false;
        if (full.Equals(NormalizePath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) return false;
        if (userData)
        {
            var allowedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games")
            }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(NormalizePath);
            return allowedRoots.Any(root => full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        return true;
    }

    private static long GetDirectorySize(string path)
    {
        long total = 0;
        var filesSeen = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (++filesSeen > 500_000) break;
                try { total += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }
        return total;
    }

    private static bool NamesLikelyMatch(string left, string right)
    {
        var a = NormalizeName(left);
        var b = NormalizeName(right);
        if (a.Length < 5 || b.Length < 5) return false;
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length > b.Length ? a : b;
        return shorter.Length >= 8 && longer.Contains(shorter, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string NormalizePath(string value)
        => Path.GetFullPath(value.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record RegisteredUninstaller(string DisplayName, string InstallLocation, string UninstallString);
}

public enum UninstallPlanKind
{
    Command,
    Uri
}

public sealed record UninstallPlan(string Label, string Target, UninstallPlanKind Kind, string Detail);

public sealed record CleanupCandidate(string Path, string Category, bool UserData, bool SafeToDelete, long SizeBytes)
{
    public string SizeLabel => SizeBytes >= 1024L * 1024 * 1024
        ? $"{SizeBytes / (1024d * 1024 * 1024):0.0} GB"
        : SizeBytes >= 1024L * 1024
            ? $"{SizeBytes / (1024d * 1024):0.0} MB"
            : $"{SizeBytes / 1024d:0} KB";
}

public sealed record CleanupResult(IReadOnlyList<string> Deleted, IReadOnlyList<string> Failed);
