using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UOTinker;

public sealed class UpdateInfo
{
    public Version Version = new(0, 0, 0, 0);
    public string Tag = "";
    public string Name = "";
    public string Url = "";
    public string Sha256 = "";
    public long Size;
    public string Notes = "";
}

public static class Updater
{
    private const string LatestUrl = "https://api.github.com/repos/" + AppInfo.Repo + "/releases/latest";
    private static readonly Regex AssetRx = new(@"^UOTinker_Setup_.*\.exe$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("UOTinker/" + AppInfo.Version);
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return h;
    }

    public static Version Current => Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0));

    private static Version Normalize(Version v) => new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    public static async Task<UpdateInfo?> CheckAsync(bool onlyNewer = true)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        string json = await Http.GetStringAsync(LatestUrl, cts.Token);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
        {
            return null;
        }
        var latest = Normalize(parsed);
        if (onlyNewer && latest <= Current)
        {
            return null;
        }
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            if (!AssetRx.IsMatch(name))
            {
                continue;
            }
            string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            return new UpdateInfo
            {
                Version = latest,
                Tag = tag,
                Name = name,
                Url = asset.GetProperty("browser_download_url").GetString() ?? "",
                Sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToLowerInvariant() : "",
                Size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                Notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
            };
        }
        return null;
    }

    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<int> progress)
    {
        if (info.Sha256.Length == 0 || info.Url.Length == 0)
        {
            throw new InvalidOperationException("Das Release enthält keine Prüfsumme; das Update wird nicht geladen.");
        }
        string path = Path.Combine(Path.GetTempPath(), info.Name);
        using (var resp = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? info.Size;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(path);
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0)
                {
                    progress.Report((int)(done * 100 / total));
                }
            }
        }
        string actual;
        await using (var fs = File.OpenRead(path))
        {
            actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
        }
        if (actual != info.Sha256)
        {
            File.Delete(path);
            throw new InvalidOperationException("Die Prüfsumme der geladenen Datei stimmt nicht mit der des Releases überein. Das Update wurde verworfen.");
        }
        return path;
    }
}
