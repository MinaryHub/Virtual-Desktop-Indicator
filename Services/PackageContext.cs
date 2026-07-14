using System.Runtime.InteropServices;
using System.Text;

namespace VirtualDesktopIndicator.Services;

/// <summary>
/// Tells whether we are running from an installed MSIX package (Microsoft Store /
/// sideloaded) versus the plain Inno Setup / portable .exe build.
///
/// The same binaries ship through both channels, so behaviour that differs between them
/// is decided at runtime rather than with a separate build:
///   • Store policy forbids self-updating, so the GitHub update check is skipped when packaged.
///   • "Run at startup" must go through the MSIX StartupTask API, not HKCU\...\Run.
/// </summary>
public static class PackageContext
{
    // GetCurrentPackageFullName returns APPMODEL_ERROR_NO_PACKAGE (15700) when the process
    // is not running inside a package; ERROR_INSUFFICIENT_BUFFER (122) means "packaged, name
    // didn't fit" — either way a non-NO_PACKAGE result proves we're packaged.
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    private static readonly Lazy<bool> _isPackaged = new(Detect);

    /// <summary>True when the running process is inside an MSIX package.</summary>
    public static bool IsPackaged => _isPackaged.Value;

    private static bool Detect()
    {
        try
        {
            int length = 0;
            int rc = GetCurrentPackageFullName(ref length, null);
            bool packaged = rc != APPMODEL_ERROR_NO_PACKAGE;
            Log.Write($"PackageContext: {(packaged ? "packaged (MSIX)" : "unpackaged")}");
            return packaged;
        }
        catch (Exception ex)
        {
            // The API is missing only on Windows 7 and earlier, which we don't target;
            // treat any failure as "unpackaged" so the plain-exe behaviour stays the default.
            Log.Write($"PackageContext: detection failed ({ex.Message}); assuming unpackaged");
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
