using System.Reflection;

namespace DeskCue.Linux.Services;

/// <summary>App version stamped at build time by the StampBuildVersion target.</summary>
public static class AppVersion
{
    public static Version Current { get; } = ReadVersion();
    public static string Display => "v" + Current;

    private static Version ReadVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info[..plus];
            if (Version.TryParse(info, out var v)) return v;
        }
        return asm.GetName().Version ?? new Version(0, 0, 0, 0);
    }
}
