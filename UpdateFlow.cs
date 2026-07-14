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
    private const string Caption = "가상 데스크톱 인디케이터";

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
                Show(owner, $"업데이트 확인에 실패했습니다.\n{result.Error}", MessageBoxImage.Warning);
            return;
        }

        if (!result.Available)
        {
            if (!silentIfNoUpdate)
                Show(owner, $"현재 최신 버전입니다. ({AppVersion.Display})", MessageBoxImage.Information);
            return;
        }

        var latest = result.LatestVersion?.ToString() ?? "새 버전";
        var answer = Ask(owner,
            $"새 버전 v{latest} 이(가) 있습니다. (현재 {AppVersion.Display})\n\n지금 다운로드하여 설치할까요?");
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
                $"업데이트 다운로드에 실패했습니다.\n{ex.Message}\n\n릴리스 페이지를 여시겠습니까?");
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
