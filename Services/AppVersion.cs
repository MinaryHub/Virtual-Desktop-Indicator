using System.Reflection;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Exposes the running app's version, stamped at build time by the
/// StampBuildVersion MSBuild target (see VirtualDesktopIndicator.csproj).
/// </summary>
public static class AppVersion
{
    /// <summary>Full version, e.g. 1.0.0.42 (Major.Minor.Patch.Build).</summary>
    public static Version Current { get; } = ReadVersion();

    /// <summary>Display string, e.g. "v1.0.0.42".</summary>
    public static string Display => "v" + Current.ToString();

    private static Version ReadVersion()
    {
        var asm = Assembly.GetExecutingAssembly();

        // Prefer the informational version (kept as a plain "1.0.0.42" string here),
        // falling back to the assembly version if it is missing or unparsable.
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip any "+<sha>" build metadata the SDK may append.
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info[..plus];
            if (Version.TryParse(info, out var v)) return v;
        }

        return asm.GetName().Version ?? new Version(0, 0, 0, 0);
    }
}
