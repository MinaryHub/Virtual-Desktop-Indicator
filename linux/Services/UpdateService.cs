using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DeskCue.Linux.Services;

public sealed record UpdateCheckResult(
    bool Available, Version? LatestVersion, string? HtmlUrl, string? Error)
{
    public static UpdateCheckResult Fail(string error) => new(false, null, null, error);
    public static UpdateCheckResult UpToDate(Version latest) => new(false, latest, null, null);
}

/// <summary>
/// Checks GitHub Releases for a newer version. On Linux the app does not
/// self-install (packaging varies by distro); it notifies the user and opens
/// the release page so they can grab the tarball.
/// </summary>
public static class UpdateService
{
    private const string Owner = "MinaryHub";
    private const string Repo = "Virtual-Desktop-Indicator";
    private static readonly string LatestApi =
        $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Repo, AppVersion.Current.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync(LatestApi, ct);
            if (!resp.IsSuccessStatusCode)
                return UpdateCheckResult.Fail($"GitHub response error: {(int)resp.StatusCode}");

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            string? tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var latest = ParseVersion(tag);
            if (latest == null) return UpdateCheckResult.Fail("Could not parse the release version.");

            string? htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            Log.Write($"update check: latest={latest} current={AppVersion.Current}");

            return IsNewer(latest, AppVersion.Current)
                ? new UpdateCheckResult(true, latest, htmlUrl, null)
                : UpdateCheckResult.UpToDate(latest);
        }
        catch (Exception ex)
        {
            Log.Write($"update check FAILED: {ex.Message}");
            return UpdateCheckResult.Fail(ex.Message);
        }
    }

    public static void OpenReleasePage(string url)
    {
        try { Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false }); }
        catch (Exception ex) { Log.Write($"xdg-open failed: {ex.Message}"); }
    }

    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        tag = tag.Trim();
        if (tag.StartsWith('v') || tag.StartsWith('V')) tag = tag[1..];
        return Version.TryParse(tag, out var v) ? v : null;
    }

    private static bool IsNewer(Version latest, Version current)
    {
        static Version Norm(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
        return Norm(latest) > Norm(current);
    }
}
