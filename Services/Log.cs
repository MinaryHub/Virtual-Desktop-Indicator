using System.IO;

namespace DeskCue.Services;

/// <summary>Minimal append-only debug log at %APPDATA%\DeskCue\debug.log.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static string Path => System.IO.Path.Combine(AppConfig.ConfigDirectory, "debug.log");

    private const long MaxBytes = 512 * 1024; // reset once the log passes ~0.5 MB

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppConfig.ConfigDirectory);
                if (File.Exists(Path) && new FileInfo(Path).Length > MaxBytes)
                    File.WriteAllText(Path, "");
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch { /* logging must never throw */ }
    }
}
