using NekoGameLauncher.Models;
using System.Diagnostics;

namespace NekoGameLauncher.Services;

public sealed class LaunchService
{
    public bool Launch(GameEntry game)
    {
        if (string.IsNullOrWhiteSpace(game.LaunchTarget)) return false;
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = game.LaunchTarget,
                Arguments = game.LaunchArguments,
                UseShellExecute = true,
                WorkingDirectory = File.Exists(game.LaunchTarget) ? Path.GetDirectoryName(game.LaunchTarget) ?? string.Empty : string.Empty
            };
            Process.Start(info);
            game.LastPlayedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch { return false; }
    }

    public bool OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}
