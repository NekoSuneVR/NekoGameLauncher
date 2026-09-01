using System.Diagnostics;

namespace NekoGameLauncher.Services;

public sealed class ConsoleRemotePlayService
{
    private static readonly string[] PsRemotePlayCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sony", "PS Remote Play", "RemotePlay.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sony", "PS Remote Play", "RemotePlay.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PS Remote Play", "RemotePlay.exe")
    ];

    public bool IsPlayStationRemotePlayInstalled => PsRemotePlayCandidates.Any(File.Exists);

    public bool OpenXboxApp()
    {
        return Start("explorer.exe", "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App");
    }

    public bool OpenXboxCloudGaming() => OpenUrl("https://www.xbox.com/play");

    public bool OpenXboxRemotePlayHelp() => OpenUrl("https://support.xbox.com/help/games-apps/game-setup-and-play/how-to-set-up-remote-play");

    public bool OpenPlayStationRemotePlay()
    {
        var exe = PsRemotePlayCandidates.FirstOrDefault(File.Exists);
        return exe is not null && Start(exe, null);
    }

    public bool OpenPlayStationRemotePlayDownload() => OpenUrl("https://remoteplay.dl.playstation.net/remoteplay/lang/en/");

    public bool OpenPlayStationRemotePlayHelp() => OpenUrl("https://www.playstation.com/remote-play/");

    private static bool OpenUrl(string url) => Start(url, null, useShellExecute: true);

    private static bool Start(string fileName, string? arguments, bool useShellExecute = true)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = useShellExecute
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
