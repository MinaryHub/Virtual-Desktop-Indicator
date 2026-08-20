using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace VirtualDesktopIndicator.Services;

/// <summary>Outcome of an update check against GitHub Releases.</summary>
public sealed record UpdateCheckResult(
    bool Available,
    Version? LatestVersion,
    string? DownloadUrl,
    string? HtmlUrl,
    string? Error)
{
    public static UpdateCheckResult Fail(string error) => new(false, null, null, null, error);
    public static UpdateCheckResult UpToDate(Version latest) => new(false, latest, null, null, null);
}

/// <summary>
/// Checks GitHub Releases for a newer version and downloads/launches the installer.
/// Uses the public "latest release" API — no token required.
/// </summary>
public static class UpdateService
{
    private const string Owner = "knoxxr";
    private const string Repo = "Virtual-Desktop-Indicator";

    private static readonly string LatestApi =
        $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // GitHub rejects requests without a User-Agent.
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Repo, AppVersion.Current.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>Queries the latest release and compares it against the running version.</summary>
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
            if (latest == null)
                return UpdateCheckResult.Fail("Could not parse the release version.");

            string? htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            string? assetUrl = PickInstallerAsset(root);

            Log.Write($"update check: latest={latest} current={AppVersion.Current} asset={assetUrl ?? "(none)"}");

            if (!IsNewer(latest, AppVersion.Current))
                return UpdateCheckResult.UpToDate(latest);

            return new UpdateCheckResult(true, latest, assetUrl, htmlUrl, null);
        }
        catch (Exception ex)
        {
            Log.Write($"update check FAILED: {ex.Message}");
            return UpdateCheckResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Downloads the installer to a temp file and launches it. Returns the path.
    /// The caller should shut the app down afterwards (the installer stops any
    /// running instance on its own, but exiting first avoids a taskkill prompt).
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(string url, CancellationToken ct = default)
    {
        string name = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrWhiteSpace(name)) name = "DeskCue-Setup.exe";
        string dest = Path.Combine(Path.GetTempPath(), name);

        using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var fs = File.Create(dest);
            await src.CopyToAsync(fs, ct);
        }

        Log.Write($"update downloaded to {dest}");
        Process.Start(new ProcessStartInfo(dest) { UseShellExecute = true });
        return dest;
    }

    /// <summary>Opens the release page in the default browser (fallback when no asset is attached).</summary>
    public static void OpenReleasePage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write($"open release page failed: {ex.Message}"); }
    }

    // --- helpers -------------------------------------------------------------

    private static string? PickInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        string? firstExe = null;
        foreach (var a in assets.EnumerateArray())
        {
            string? name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
            string? dl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (name == null || dl == null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;

            firstExe ??= dl;
            // Prefer the Setup installer if several .exe assets exist.
            if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                return dl;
        }
        return firstExe;
    }

    /// <summary>Parses "v1.2.0" / "1.2.0" into a Version (null if unparsable).</summary>
    private static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        tag = tag.Trim();
        if (tag.StartsWith('v') || tag.StartsWith('V')) tag = tag[1..];
        return Version.TryParse(tag, out var v) ? v : null;
    }

    /// <summary>Compares Major.Minor.Patch only, ignoring the local build/revision component.</summary>
    private static bool IsNewer(Version latest, Version current)
    {
        static Version Norm(Version v) =>
            new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
        return Norm(latest) > Norm(current);
    }
}
