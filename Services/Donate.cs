using System.Diagnostics;

namespace DeskCue.Services;

/// <summary>
/// "Support development" link — opens the project's GitHub Sponsors page in the user's
/// default browser.
///
/// The app deliberately never handles money itself: no payment UI, no in-app purchase, no
/// account. It is a plain outbound link, which is why it can ship in the MSIX/Store build
/// too (unlike the self-update flow, which Store policy replaces — see
/// <see cref="PackageContext"/>).
/// </summary>
public static class Donate
{
    /// <summary>
    /// GitHub Sponsors page for the project owner. Update this together with
    /// <c>UpdateService.Owner</c> if the GitHub account ever changes.
    /// </summary>
    public const string Url = "https://github.com/sponsors/MinaryHub";

    /// <summary>Opens <see cref="Url"/> in the default browser; never throws.</summary>
    public static void Open()
    {
        try
        {
            // UseShellExecute is required to hand an http(s) URL to the shell; without it
            // .NET tries to exec the URL as a program and throws.
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
            Log.Write("opened sponsors page");
        }
        catch (Exception ex)
        {
            Log.Write($"open sponsors page failed: {ex.Message}");
        }
    }
}
