using System.IO;

namespace DeskCue.Linux.Services;

/// <summary>Best-effort debug log under the config directory.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Path =
        System.IO.Path.Combine(AppConfig.ConfigDirectory, "debug.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppConfig.ConfigDirectory);
                File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch { /* logging is best-effort */ }
    }
}
