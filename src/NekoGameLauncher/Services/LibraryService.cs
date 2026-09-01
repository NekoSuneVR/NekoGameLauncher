using NekoGameLauncher.Models;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class LibraryService
{
    private readonly IReadOnlyList<IGameLibraryProvider> _providers;
    private readonly string _cacheFile;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public LibraryService()
    {
        _providers = new IGameLibraryProvider[]
        {
            new SteamGameProvider(),
            new EpicGameProvider(),
            new RegistryPublisherGameProvider("EA / Origin", ["Electronic Arts", "EA Games"], "EA app"),
            new RegistryPublisherGameProvider("Ubisoft Connect", ["Ubisoft"], "Ubisoft Connect", "Ubisoft Game Launcher"),
            new RegistryPublisherGameProvider("GOG Galaxy", ["GOG.com", "GOG Sp. z o.o."], "GOG GALAXY"),
            new RegistryPublisherGameProvider("Battle.net", ["Blizzard Entertainment", "Activision Blizzard"], "Battle.net"),
            new RegistryPublisherGameProvider("Rockstar Games", ["Rockstar Games"], "Rockstar Games Launcher", "Social Club"),
            new RegistryPublisherGameProvider("Riot Games", ["Riot Games"], "Riot Client"),
            new XboxGameProvider(),
            new KnownGameProvider()
        };
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NekoGameLauncher");
        Directory.CreateDirectory(folder);
        _cacheFile = Path.Combine(folder, "library.json");
    }

    public async Task<IReadOnlyList<GameEntry>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<GameEntry>();
        foreach (var provider in _providers)
        {
            try { all.AddRange(await provider.ScanAsync(cancellationToken)); }
            catch { }
        }

        var unique = all
            .GroupBy(g => $"{g.Launcher}|{(string.IsNullOrWhiteSpace(g.SourceId) ? g.InstallPath + g.Name : g.SourceId)}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        await File.WriteAllTextAsync(_cacheFile, JsonSerializer.Serialize(unique, _json), cancellationToken);
        return unique;
    }

    public async Task<IReadOnlyList<GameEntry>> LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cacheFile)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(_cacheFile, cancellationToken);
            return JsonSerializer.Deserialize<List<GameEntry>>(json) ?? [];
        }
        catch { return []; }
    }

    public async Task SaveAsync(IEnumerable<GameEntry> games, CancellationToken cancellationToken = default)
        => await File.WriteAllTextAsync(_cacheFile, JsonSerializer.Serialize(games, _json), cancellationToken);
}
