namespace VirtualDesktopIndicator.Linux.Services;

/// <summary>
/// Shared update-check UI flow. On Linux the app does not self-install; on
/// consent it opens the release page so the user can fetch the new tarball.
/// </summary>
public static class UpdateFlow
{
    public static async Task HandleAsync(UpdateCheckResult result, bool silentIfNoUpdate)
    {
        if (result.Error != null)
        {
            if (!silentIfNoUpdate)
                await Dialogs.ShowInfoAsync($"Failed to check for updates.\n{result.Error}");
            return;
        }

        if (!result.Available)
        {
            if (!silentIfNoUpdate)
                await Dialogs.ShowInfoAsync($"You are on the latest version. ({AppVersion.Display})");
            return;
        }

        var latest = result.LatestVersion?.ToString() ?? "new version";
        bool open = await Dialogs.ConfirmAsync(
            $"A new version v{latest} is available. (current {AppVersion.Display})\n\n" +
            "Open the release page to download it?");

        if (open && !string.IsNullOrEmpty(result.HtmlUrl))
            UpdateService.OpenReleasePage(result.HtmlUrl);
    }
}
