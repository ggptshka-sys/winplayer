using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPlayer.Services;

public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class UpdateCheckResult
{
    public required bool UpdateAvailable { get; init; }
    public required Version CurrentVersion { get; init; }
    public Version? RemoteVersion { get; init; }
    public UpdateManifest? Manifest { get; init; }
    public string? Error { get; init; }
}

public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    public static string GetCurrentVersionDisplay()
    {
        var v = GetCurrentVersion();
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    public static string? ResolveFeedUrl()
    {
        // 1) рядом с exe: update-feed.url
        var local = Path.Combine(AppContext.BaseDirectory, "update-feed.url");
        if (File.Exists(local))
        {
            var url = File.ReadAllText(local).Trim();
            if (IsUsableFeed(url)) return url;
        }

        // 2) %AppData%\WinPlayer\update-feed.url — можно поменять без пересборки
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinPlayer",
            "update-feed.url");
        if (File.Exists(appData))
        {
            var url = File.ReadAllText(appData).Trim();
            if (IsUsableFeed(url)) return url;
        }

        return null;
    }

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();
        var feed = ResolveFeedUrl();
        if (feed is null)
        {
            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                CurrentVersion = current,
                Error = "Не задан URL ленты обновлений (update-feed.url)."
            };
        }

        try
        {
            using var response = await Http.GetAsync(feed, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct);
            if (manifest is null || !Version.TryParse(NormalizeVersion(manifest.Version), out var remote))
            {
                return new UpdateCheckResult
                {
                    UpdateAvailable = false,
                    CurrentVersion = current,
                    Error = "Некорректный version.json"
                };
            }

            var available = remote > TrimRevision(current);
            return new UpdateCheckResult
            {
                UpdateAvailable = available,
                CurrentVersion = current,
                RemoteVersion = remote,
                Manifest = manifest
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                UpdateAvailable = false,
                CurrentVersion = current,
                Error = ex.Message
            };
        }
    }

    public static void OpenDownload(UpdateManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Url)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = manifest.Url,
            UseShellExecute = true
        });
    }

    private static bool IsUsableFeed(string url) =>
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        && !url.Contains("OWNER/REPO", StringComparison.Ordinal);

    private static string NormalizeVersion(string version)
    {
        var v = version.Trim().TrimStart('v', 'V');
        var parts = v.Split('.');
        if (parts.Length == 2) return v + ".0";
        if (parts.Length == 1) return v + ".0.0";
        return v;
    }

    private static Version TrimRevision(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinPlayer-Updater/1.0");
        return client;
    }
}
