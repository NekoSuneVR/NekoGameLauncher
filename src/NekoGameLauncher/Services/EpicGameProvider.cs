using NekoGameLauncher.Models;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class EpicGameProvider : IGameLibraryProvider
{
    public string Name => "Epic Games";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameEntry>();
        var manifestRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifestRoot)) return Task.FromResult<IReadOnlyList<GameEntry>>(games);

        foreach (var file in Directory.EnumerateFiles(manifestRoot, "*.item", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                var title = Get(root, "DisplayName");
                var install = Get(root, "InstallLocation");
                var appName = Get(root, "AppName");
                var catalog = Get(root, "CatalogItemId");
                var ns = Get(root, "CatalogNamespace");
                var executable = Get(root, "LaunchExecutable");
                var args = Get(root, "LaunchCommand");
                if (string.IsNullOrWhiteSpace(title)) continue;

                var uri = string.Empty;
                if (!string.IsNullOrWhiteSpace(ns) && !string.IsNullOrWhiteSpace(catalog) && !string.IsNullOrWhiteSpace(appName))
                {
                    var identity = Uri.EscapeDataString($"{ns}:{catalog}:{appName}");
                    uri = $"com.epicgames.launcher://apps/{identity}?action=launch&silent=true";
                }
                var exePath = string.IsNullOrWhiteSpace(executable) ? string.Empty : Path.Combine(install, executable);

                games.Add(new GameEntry
                {
                    Name = title,
                    Launcher = Name,
                    SourceId = appName,
                    InstallPath = install,
                    LaunchTarget = !string.IsNullOrWhiteSpace(uri) ? uri : (File.Exists(exePath) ? exePath : string.Empty),
                    LaunchArguments = args,
                    IsInstalled = string.IsNullOrWhiteSpace(install) || Directory.Exists(install)
                });
            }
            catch { }
        }

        return Task.FromResult<IReadOnlyList<GameEntry>>(games);
    }

    private static string Get(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
