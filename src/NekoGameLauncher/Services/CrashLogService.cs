namespace NekoGameLauncher.Services;

public static class CrashLogService
{
    public static string Write(string source, Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NekoGameLauncher", "logs");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"crash-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {source}\n{exception}\n\n");
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }
}
