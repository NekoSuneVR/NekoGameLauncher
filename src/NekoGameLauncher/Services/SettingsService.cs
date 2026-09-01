using NekoGameLauncher.Models;
using System.Text.Json;

namespace NekoGameLauncher.Services;

public sealed class SettingsService
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NekoGameLauncher");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public Task SaveAsync(AppSettings settings)
        => File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings, _options));
}
