using Microsoft.Win32;
using NekoGameLauncher.Models;

namespace NekoGameLauncher.Services;

public sealed class RegistryPublisherGameProvider : IGameLibraryProvider
{
    private readonly string[] _publisherTerms;
    private readonly string[] _excludedNames;
    public string Name { get; }

    public RegistryPublisherGameProvider(string name, string[] publisherTerms, params string[] excludedNames)
    {
        Name = name;
        _publisherTerms = publisherTerms;
        _excludedNames = excludedNames;
    }

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in EnumerateUninstallEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_publisherTerms.Any(term => entry.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;
            if (_excludedNames.Any(term => entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;
            if (string.IsNullOrWhiteSpace(entry.Name) || !seen.Add(entry.Name)) continue;

            var target = ExtractExecutable(entry.DisplayIcon);
            if (!File.Exists(target)) target = FindLikelyExecutable(entry.InstallLocation, entry.Name);

            games.Add(new GameEntry
            {
                Name = entry.Name,
                Launcher = Name,
                SourceId = entry.RegistryKey,
                InstallPath = entry.InstallLocation,
                LaunchTarget = target,
                IconPath = File.Exists(ExtractExecutable(entry.DisplayIcon)) ? ExtractExecutable(entry.DisplayIcon) : string.Empty,
                IsInstalled = string.IsNullOrWhiteSpace(entry.InstallLocation) || Directory.Exists(entry.InstallLocation)
            });
        }
        return Task.FromResult<IReadOnlyList<GameEntry>>(games);
    }

    private static IEnumerable<RegistryEntry> EnumerateUninstallEntries()
    {
        var roots = new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
        var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
        foreach (var root in roots)
        foreach (var view in views)
        {
            RegistryKey? baseKey = null;
            RegistryKey? uninstall = null;
            try
            {
                baseKey = RegistryKey.OpenBaseKey(root, view);
                uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    RegistryKey? key = null;
                    try
                    {
                        key = uninstall.OpenSubKey(name);
                        var displayName = key?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(displayName)) continue;
                        yield return new RegistryEntry(
                            name,
                            displayName,
                            key?.GetValue("Publisher")?.ToString() ?? string.Empty,
                            key?.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                            key?.GetValue("DisplayIcon")?.ToString() ?? string.Empty);
                    }
                    finally { key?.Dispose(); }
                }
            }
            finally
            {
                uninstall?.Dispose();
                baseKey?.Dispose();
            }
        }
    }

    private static string ExtractExecutable(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Trim().Trim('"');
        var comma = cleaned.LastIndexOf(',');
        if (comma > 0) cleaned = cleaned[..comma].Trim().Trim('"');
        return cleaned;
    }

    private static string FindLikelyExecutable(string installLocation, string gameName)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation)) return string.Empty;
        try
        {
            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).ToList();
            if (exes.Count == 0) return string.Empty;
            var normalizedName = new string(gameName.Where(char.IsLetterOrDigit).ToArray());
            return exes.OrderByDescending(path =>
                new string(Path.GetFileNameWithoutExtension(path).Where(char.IsLetterOrDigit).ToArray())
                    .Contains(normalizedName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private sealed record RegistryEntry(string RegistryKey, string Name, string Publisher, string InstallLocation, string DisplayIcon);
}
