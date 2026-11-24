using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nova;

internal static partial class Policy
{
    public static bool WebUrl(string? text, bool allowHttp = false) =>
        Uri.TryCreate(text, UriKind.Absolute, out var u) && u.UserInfo.Length == 0 &&
        (u.Scheme == "https" || allowHttp && u.Scheme == "http");

    public static string Destination(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return "";
        if (WebUrl(text, true)) return text;
        // Never turn an explicitly supplied privileged scheme into a navigation.
        if (Regex.IsMatch(text, @"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase))
            throw new ArgumentException("Only HTTPS website addresses are supported (HTTP is optional in Settings).");
        if (!text.Any(char.IsWhiteSpace) && WebUrl("https://" + text) &&
            (new Uri("https://" + text).Host.Contains('.') || text == "localhost")) return "https://" + text;
        return "https://duckduckgo.com/?q=" + Uri.EscapeDataString(text);
    }

    public static string SafeFileName(string value)
    {
        value = value.Replace('\\', '/').Split('/').Last();
        value = Regex.Replace(value, "[<>:\"/\\\\|?*\\x00-\\x1F\\x7F\\u202A-\\u202E\\u2066-\\u2069]", "_").Trim().TrimEnd('.');
        if (value.Length == 0) value = "download";
        if (Regex.IsMatch(value, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", RegexOptions.IgnoreCase)) value = "_" + value;
        return value.Length > 160 ? value[..120] + Path.GetExtension(value)[..Math.Min(20, Path.GetExtension(value).Length)] : value;
    }

    public static bool RiskyFile(string name) => Regex.IsMatch(name,
        @"\.(exe|msi|msix|com|scr|bat|cmd|ps1|vbs|js|jse|wsf|hta|lnk|url|reg|iso|img|dll|jar)$", RegexOptions.IgnoreCase);

    public static List<PageFile> ParseLinks(string json, bool allowHttp) =>
        (JsonSerializer.Deserialize<List<PageFile>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
        .Where(x => x.Url.Length <= 8192 && WebUrl(x.Url, allowHttp))
        .DistinctBy(x => x.Url).Take(100).Select(x => x with { Name = SafeFileName(x.Name) }).ToList();
}

internal record PageFile(string Name, string Url);

internal sealed record Preferences
{
    public string Theme { get; set; } = "Graphite";
    public string Accent { get; set; } = "Sage";
    public bool Private { get; set; } = true;
    public bool AllowHttp { get; set; }
    public bool Background { get; set; }
    public int SleepMinutes { get; set; } = 5;
    public static Preferences Load(string file)
    {
        try { return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, file, true);
    }
}
