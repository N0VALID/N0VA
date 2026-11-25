using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nova;
internal record AppUpdate(Version Version, string Url);
internal static class UpdatePolicy
{
    public const string Current = "0.4.0";
    public const string Repository = "N0VALID/N0VA";
    public static AppUpdate? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
        var found = new List<AppUpdate>();
        foreach (var release in doc.RootElement.EnumerateArray()) {
            if (release.ValueKind != JsonValueKind.Object || release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) continue;
            if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) continue;
            foreach (var asset in assets.EnumerateArray()) {
                if (asset.ValueKind != JsonValueKind.Object || !asset.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || !asset.TryGetProperty("browser_download_url", out var url) || url.ValueKind != JsonValueKind.String) continue;
                var match = Regex.Match(name.GetString()!, @"\AN0VA-Browser-Setup-(\d+\.\d+\.\d+)-x64\.exe\z");
                if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version) || version <= Version.Parse(Current)) continue;
                var text = url.GetString()!;
                if (!Policy.WebUrl(text) || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Host != "github.com" || uri.Port != 443 || uri.Query.Length > 0 || uri.Fragment.Length > 0) continue;
                var parts = uri.AbsolutePath.Split('/');
                if (parts.Length != 7 || parts[1] != "N0VALID" || parts[2] != "N0VA" || parts[3] != "releases" || parts[4] != "download" || parts[5].Length == 0 || parts[6] != name.GetString()) continue;
                found.Add(new(version, text));
            }
        }
        return found.OrderByDescending(x => x.Version).FirstOrDefault();
    }
    public static async Task<AppUpdate?> Fetch(string connection)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        if (connection.Length > 0) handler.Proxy = new WebProxy(ConnectionPolicy.Validate(connection));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("N0VA-Browser/" + Current);
        return Parse(await client.GetStringAsync("https://api.github.com/repos/" + Repository + "/releases?per_page=30"));
    }
}
