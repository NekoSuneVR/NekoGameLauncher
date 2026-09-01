using NekoGameLauncher.Models;

namespace NekoGameLauncher.Services;

public sealed class XboxGameProvider : IGameLibraryProvider
{
    public string Name => "Xbox / Microsoft Store";

    public Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameEntry>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> folders;
            try { folders = Directory.EnumerateDirectories(root).ToArray(); }
            catch { continue; }

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var content = Directory.Exists(Path.Combine(folder, "Content")) ? Path.Combine(folder, "Content") : folder;
                    var helper = Path.Combine(content, "gamelaunchhelper.exe");
                    var target = File.Exists(helper)
                        ? helper
                        : Directory.EnumerateFiles(content, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? string.Empty;
                    games.Add(new GameEntry
                    {
                        Name = Path.GetFileName(folder),
                        Launcher = Name,
                        SourceId = folder,
                        InstallPath = content,
                        LaunchTarget = target,
                        IsInstalled = true
                    });
                }
                catch { }
            }
        }
        return Task.FromResult<IReadOnlyList<GameEntry>>(games);
    }
}
