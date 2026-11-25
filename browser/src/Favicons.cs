using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace Nova;
internal sealed partial class BrowserWindow
{
    readonly ToolStrip favoriteBar = new() { Name = "Favorite icons", AutoSize = false, Dock = DockStyle.None, GripStyle = ToolStripGripStyle.Hidden, Padding = Padding.Empty };
    readonly ToolTip favoriteTips = new();
    internal int FavoriteIconCount => favoriteBar.Items.Count;
    internal void OpenFavoriteIcon(int index) => favoriteBar.Items[index].PerformClick();
    string IconPath(string url) => Path.Combine(root, "Favicons", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new Uri(url).GetLeftPart(UriPartial.Authority)))) + ".png");
    Image? ReadFavoriteIcon(string url)
    {
        try { var path = IconPath(url); if (!File.Exists(path) || new FileInfo(path).Length > 65536) return null; using var source = Image.FromFile(path); return new Bitmap(source, 24, 24); } catch { return null; }
    }
    void UpdateFavorites()
    {
        foreach (var item in favoriteBar.Items.Cast<ToolStripItem>().ToArray()) { item.Image?.Dispose(); item.Dispose(); }
        favoriteBar.Items.Clear(); favoriteBar.BackColor = Surface; favoriteBar.ForeColor = Ink;
        foreach (var favorite in library.Bookmarks.Take(20)) {
            var image = ReadFavoriteIcon(favorite.Url);
            var button = new ToolStripButton { Image = image, Text = image == null ? new Uri(favorite.Url).IdnHost[..1].ToUpperInvariant() : "", DisplayStyle = image == null ? ToolStripItemDisplayStyle.Text : ToolStripItemDisplayStyle.Image, AutoSize = false, Width = 34, Height = 30, AccessibleName = favorite.Title, ToolTipText = favorite.Title + "\n" + new Uri(favorite.Url).IdnHost };
            button.Click += (_, _) => _ = Navigate(Current!, favorite.Url); favoriteBar.Items.Add(button);
        }
        favoriteBar.Visible = prefs.ShowFavorites && favoriteBar.Items.Count > 0;
        chrome.Height = favoriteBar.Visible ? 90 : 58; Fit();
    }
    async Task CaptureFavicon(SiteTab tab)
    {
        var core = tab.View?.CoreWebView2; if (core == null || !Policy.WebUrl(core.Source, prefs.AllowHttp)) return;
        var source = core.Source;
        try {
            using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer)) > 0) { if (bytes.Length + read > 65536) return; bytes.Write(buffer, 0, read); }
            if (tab.IsDisposed || core.Source != source || bytes.Length == 0) return;
            bytes.Position = 0; using var decoded = Image.FromStream(bytes);
            if (decoded.Width > 512 || decoded.Height > 512) return;
            tab.Favicon?.Dispose(); tab.Favicon = new Bitmap(decoded, 24, 24); SaveFavicon(tab);
        } catch (Exception) { /* A missing/invalid website icon uses its initial instead. */ }
    }
    void SaveFavicon(SiteTab tab)
    {
        if (tab.Favicon == null || tab.View?.CoreWebView2 == null) return;
        var source = tab.View.CoreWebView2.Source;
        if (!library.Bookmarks.Any(b => new Uri(b.Url).GetLeftPart(UriPartial.Authority) == new Uri(source).GetLeftPart(UriPartial.Authority))) return;
        try { var path = IconPath(source); Directory.CreateDirectory(Path.GetDirectoryName(path)!); tab.Favicon.Save(path, System.Drawing.Imaging.ImageFormat.Png); UpdateFavorites(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
