using System.Text.Json;

namespace Nova;
internal record Bookmark(string Title, string Url);
internal sealed class Transfer
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Source { get; set; } = "";
    public string State { get; set; } = "Interrupted";
    public DateTime Started { get; set; } = DateTime.UtcNow;
}
internal sealed class Library
{
    public List<Bookmark> Bookmarks { get; set; } = [];
    public List<Transfer> Transfers { get; set; } = [];
    public static Library Load(string file)
    {
        try {
            var library = JsonSerializer.Deserialize<Library>(File.ReadAllText(file)) ?? new();
            library.Bookmarks = (library.Bookmarks ?? []).Where(b => b != null && Policy.WebUrl(b.Url, true)).Take(300).ToList();
            library.Transfers = (library.Transfers ?? []).Where(d => d != null).Take(100).ToList();
            foreach (var item in library.Transfers.Where(d => d.State is "InProgress" or "Paused")) item.State = "Interrupted";
            return library;
        } catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save(string file)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(this)); File.Move(file + ".tmp", file, true);
    }
}

