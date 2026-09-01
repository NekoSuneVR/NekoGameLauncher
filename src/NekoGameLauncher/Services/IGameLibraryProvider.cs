using NekoGameLauncher.Models;

namespace NekoGameLauncher.Services;

public interface IGameLibraryProvider
{
    string Name { get; }
    Task<IReadOnlyList<GameEntry>> ScanAsync(CancellationToken cancellationToken = default);
}
