namespace Nova;

internal sealed partial class BrowserWindow
{
    void SaveLibrary() { try { library.Save(Path.Combine(root, "library.json")); } catch (Exception e) { Notice("Could not save library: " + e.Message); } }
    internal void BookmarkCurrent()
    {
        if (Current?.Ready != true) return;
        var c = Current.View!.CoreWebView2; if (!Policy.WebUrl(c.Source, prefs.AllowHttp)) return;
        var existing = library.Bookmarks.FirstOrDefault(b => b.Url == c.Source);
        if (existing != null) library.Bookmarks.Remove(existing);
        else { if (library.Bookmarks.Count >= 300) { Notice("Remove a favorite before adding another (limit 300)."); return; } library.Bookmarks.Add(new(c.DocumentTitle, c.Source)); }
        SaveLibrary(); Sync(); RefreshHomePages();
    }
    void OpenFavorites()
    {
        using var dialog = new Form { Text = "N0VA · Favorites", Size = new Size(670, 530), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; dialog.Controls.Add(wrap);
        wrap.Controls.Add(Caption("Favorites", 23));
        if (library.Bookmarks.Count == 0) wrap.Controls.Add(Caption("Press the star beside the address bar to save a website. Favorites stay even in temporary sessions."));
        foreach (var favorite in library.Bookmarks.ToArray()) {
            var row = new FlowLayoutPanel { Width = 590, Height = 82 };
            var open = ActionButton(favorite.Title.Length > 50 ? favorite.Title[..50] + "…" : favorite.Title, () => { dialog.Close(); _ = Navigate(AddTab(), favorite.Url); }); open.MaximumSize = new Size(440, 40); open.UseMnemonic = false;
            row.Controls.Add(open); row.Controls.Add(ActionButton("Remove", () => { library.Bookmarks.Remove(favorite); SaveLibrary(); row.Dispose(); Sync(); RefreshHomePages(); }));
            wrap.Controls.Add(row);
        }
        dialog.ShowDialog(this);
    }
    internal void OpenCredits()
    {
        var tab = AddTab(); tab.Home?.Dispose(); tab.Home = null; tab.Text = "Credits";
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(52), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Surface, ForeColor = Ink };
        panel.Controls.Add(Caption("Made by N0VALID", 28));
        foreach (var url in new[] { "https://github.com/N0VALID/N0VA/", "https://guns.lol/n0valid" }) {
            var link = new LinkLabel { Text = url, AutoSize = true, LinkColor = Accent, ActiveLinkColor = Ink, Margin = new Padding(0, 14, 0, 14) };
            link.LinkClicked += async (_, _) => await Navigate(AddTab(), url); panel.Controls.Add(link);
        }
        panel.Controls.Add(Caption("Built with Microsoft WebView2 and uBlock Origin Lite. Independent of Brave, LibreWolf and the uBlock Origin project.", 10));
        panel.Controls.Add(ActionButton("Back to home", () => AddTab()));
        tab.Home = panel; tab.Controls.Add(panel); Sync();
    }
}
