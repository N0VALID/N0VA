using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nova;
internal static class ReleaseDiscovery
{
    public static (string Repository, string Api)? Target(string url)
    {
        if (!Policy.WebUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Host != "github.com" || u.Port != 443) return null;
        var parts = u.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 3 || parts[2] != "releases" || !Regex.IsMatch(parts[0], @"\A[a-zA-Z0-9-]+\z") || !Regex.IsMatch(parts[1], @"\A[a-zA-Z0-9_.-]+\z")) return null;
        if (parts.Length > 3 && parts[3] is not ("latest" or "tag")) return null;
        var repo = parts[0] + "/" + parts[1];
        var tail = parts.Length > 4 && parts[3] == "tag" ? "tags/" + Uri.EscapeDataString(Uri.UnescapeDataString(string.Join('/', parts.Skip(4)))) : "latest";
        return (repo, "https://api.github.com/repos/" + repo + "/releases/" + tail);
    }
    public static List<PageFile> Parse(string json, string repo)
    {
        using var doc = JsonDocument.Parse(json); var results = new List<PageFile>();
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return results;
        foreach (var asset in assets.EnumerateArray().Take(150)) {
            if (!asset.TryGetProperty("browser_download_url", out var url) || url.ValueKind != JsonValueKind.String) continue;
            var text = url.GetString()!;
            if (!Policy.WebUrl(text) || !text.StartsWith("https://github.com/" + repo + "/releases/download/", StringComparison.Ordinal)) continue;
            var name = asset.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : new Uri(text).Segments.Last();
            results.Add(new(Policy.SafeFileName(name), text));
        }
        return results.DistinctBy(x => x.Url).OrderByDescending(x => Regex.IsMatch(x.Name, @"\.(exe|msi|msix)$", RegexOptions.IgnoreCase)).ThenBy(x => x.Name).ToList();
    }
    public static async Task<List<PageFile>> Fetch(string url, string connection)
    {
        var target = Target(url) ?? throw new ArgumentException("This is not a supported GitHub release link.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        if (connection.Length > 0) handler.Proxy = new WebProxy(ConnectionPolicy.Validate(connection));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("N0VA-Browser/0.4.2");
        return Parse(await client.GetStringAsync(target.Api), target.Repository);
    }
}
