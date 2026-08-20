using System.Windows;
using VirtualDesktopIndicator.Services;
using MessageBox = System.Windows.MessageBox;

namespace VirtualDesktopIndicator;

/// <summary>
/// Shared UI flow for update checks: notify the user of a new version and,
/// on consent, download the installer and exit so it can replace the app.
/// </summary>
public static class UpdateFlow
{
    private const string Caption = "DeskCue";

    /// <summary>
    /// Reacts to a completed <see cref="UpdateService.CheckAsync"/> result.
    /// When <paramref name="silentIfNoUpdate"/> is true (startup checks), stays
    /// quiet unless a newer version is actually available.
    /// </summary>
    public static async Task HandleAsync(UpdateCheckResult result, Window? owner, bool silentIfNoUpdate)
    {
        if (result.Error != null)
        {
            if (!silentIfNoUpdate)
                Show(owner, $"Failed to check for updates.\n{result.Error}", MessageBoxImage.Warning);
            return;
        }

        if (!result.Available)
        {
            if (!silentIfNoUpdate)
                Show(owner, $"You are on the latest version. ({AppVersion.Display})", MessageBoxImage.Information);
            return;
        }

        var latest = result.LatestVersion?.ToString() ?? "new version";
        var answer = Ask(owner,
            $"A new version v{latest} is available. (current {AppVersion.Display})\n\nDownload and install it now?");
        if (answer != MessageBoxResult.Yes) return;

        // No installer asset attached → just open the release page.
        if (string.IsNullOrEmpty(result.DownloadUrl))
        {
            if (!string.IsNullOrEmpty(result.HtmlUrl)) UpdateService.OpenReleasePage(result.HtmlUrl);
            return;
        }

        try
        {
            await UpdateService.DownloadInstallerAsync(result.DownloadUrl);
            // Installer launched; exit so it can overwrite the running files cleanly.
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Write($"update download/install failed: {ex.Message}");
            var retry = Ask(owner,
                $"Failed to download the update.\n{ex.Message}\n\nOpen the release page?");
            if (retry == MessageBoxResult.Yes && !string.IsNullOrEmpty(result.HtmlUrl))
                UpdateService.OpenReleasePage(result.HtmlUrl);
        }
    }

    // The app has no normally-activatable window, so parent dialogs to a transient
    // topmost owner to guarantee they surface in front instead of behind everything.
    private static void Show(Window? owner, string text, MessageBoxImage icon)
        => WithOwner(owner, o => MessageBox.Show(o, text, Caption, MessageBoxButton.OK, icon));

    private static MessageBoxResult Ask(Window? owner, string text)
        => WithOwner(owner, o => MessageBox.Show(o, text, Caption,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes));

    private static T WithOwner<T>(Window? owner, Func<Window, T> show)
    {
        if (owner != null) return show(owner);

        var transient = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Left = -2000,
            Top = -2000,
        };
        transient.Show();
        try { return show(transient); }
        finally { transient.Close(); }
    }
}
