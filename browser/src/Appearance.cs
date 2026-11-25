using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Nova;

internal sealed class HomeSurface : SmoothPanel
{
    public Image? Wallpaper;
    public bool FillImage;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Wallpaper == null || Width == 0 || Height == 0) return;
        var scale = FillImage ? Math.Max((float)Width / Wallpaper.Width, (float)Height / Wallpaper.Height) : Math.Min((float)Width / Wallpaper.Width, (float)Height / Wallpaper.Height);
        var w = Wallpaper.Width * scale; var h = Wallpaper.Height * scale;
        e.Graphics.DrawImage(Wallpaper, (Width - w) / 2, (Height - h) / 2, w, h);
    }
}
internal sealed class IntroSurface : SmoothPanel
{
    readonly Stopwatch elapsed = Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    readonly Color ink;
    public IntroSurface(Color surface, Color color)
    {
        Dock = DockStyle.Fill; BackColor = surface; ink = color; TabStop = true;
        timer.Tick += (_, _) => { if (elapsed.ElapsedMilliseconds > 1200) { Dispose(); return; } Invalidate(); }; timer.Start();
        Click += (_, _) => Dispose(); KeyDown += (_, _) => Dispose();
        Disposed += (_, _) => timer.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var progress = Math.Min(1, elapsed.Elapsed.TotalSeconds / 1.2);
        var bounce = (1 - Math.Cos(progress * Math.PI * 4)) / 2;
        using var font = new Font("Segoe UI", 76, FontStyle.Bold);
        var size = TextRenderer.MeasureText("N", font);
        var x = (int)(24 + bounce * Math.Max(0, Width - size.Width - 48));
        TextRenderer.DrawText(e.Graphics, "N", font, new Point(x, Math.Max(0, (Height - size.Height) / 2)), ink, TextFormatFlags.NoPadding);
    }
}

internal sealed partial class BrowserWindow
{
    Image? wallpaper;
    readonly List<Label> counters = [];
    long blockedRequests;
    internal long BlockedRequests => blockedRequests;
    static Button ActionButton(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(100, 34), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 12, 8), Cursor = Cursors.Hand, UseMnemonic = false };
        b.FlatAppearance.BorderColor = Color.FromArgb(100, 115, 103); b.Click += (_, _) => action(); return b;
    }
    static Label Caption(string text, float size = 10) => new() { Text = text, AutoSize = true, MaximumSize = new Size(570, 0), Font = new Font("Segoe UI", size), Margin = new Padding(0, 8, 0, 8), UseMnemonic = false };
    void ApplyTheme()
    {
        SuspendLayout(); BackColor = Surface; ForeColor = Ink; toolbar.BackColor = Surface; toolbar.ForeColor = Ink;
        statusBar.BackColor = Surface; statusBar.ForeColor = Ink; downloads.BackColor = Surface; downloads.ForeColor = Ink;
        address.BackColor = Surface; address.ForeColor = Ink; tabs.Theme(Surface, Ink, Accent);
        foreach (var tab in tabs.TabPages) tab.BackColor = Surface;
        RefreshHomePages(); ResumeLayout(true);
    }
    void RefreshHomePages() { foreach (var tab in tabs.TabPages.ToArray()) if (tab.Home != null) ShowHome(tab); }
    internal void PlayIntro() { var intro = new IntroSurface(Surface, Accent); Controls.Add(intro); intro.BringToFront(); intro.Focus(); }
    void UpdateCounters()
    {
        counters.RemoveAll(c => c.IsDisposed);
        foreach (var label in counters) label.Text = $"{blockedRequests:N0}   blocked requests  ·  Ads & trackers\nThis session · reported network blocks, not a count of unique ads";
    }
    void ShowHome(SiteTab tab)
    {
        tab.Home?.Dispose();
        if (prefs.Background && wallpaper == null) try { using var image = Image.FromFile(Path.Combine(root, "background.jpg")); wallpaper = new Bitmap(image); } catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException) { }
        var home = new HomeSurface { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Ink, Wallpaper = prefs.Background ? wallpaper : null, FillImage = prefs.BackgroundFit == "Fill" };
        var center = new FlowLayoutPanel { Width = 620, Height = 275, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24), BackColor = Surface, ForeColor = Ink };
        center.Controls.Add(Caption("N 0 V A", 44));
        var search = new TextBox { Width = 560, Font = new Font("Segoe UI", 14), PlaceholderText = "Search or enter an address", BackColor = Surface, ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Search or enter address", Margin = new Padding(0, 16, 0, 8) };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; address.Text = search.Text; await NavigateInput(); } }; center.Controls.Add(search);
        if (prefs.ShowFavorites) {
            var favorites = new FlowLayoutPanel { Width = 560, Height = 90, WrapContents = true };
            foreach (var favorite in library.Bookmarks.Take(6)) {
                var label = new Uri(favorite.Url).IdnHost; var b = ActionButton(label, () => _ = Navigate(tab, favorite.Url)); b.MaximumSize = new Size(170, 34); favorites.Controls.Add(b);
            }
            center.Controls.Add(favorites);
        }
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 105, BackColor = Surface, ForeColor = Ink, Padding = new Padding(18, 8, 18, 8) };
        var credits = ActionButton("Credits", OpenCredits); credits.Dock = DockStyle.Left;
        var personalize = ActionButton("Personalize", () => OpenSettings("Appearance")); personalize.Dock = DockStyle.Right;
        var count = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10), UseMnemonic = false }; counters.Add(count);
        footer.Controls.Add(count); footer.Controls.Add(credits); footer.Controls.Add(personalize); UpdateCounters();
        home.Controls.Add(center); home.Controls.Add(footer);
        if (prefs.ShowClock) home.Controls.Add(new Label { Dock = DockStyle.Top, Height = 48, Text = DateTime.Now.ToString("ddd, MMM d  ·  HH:mm"), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(12), BackColor = Surface, ForeColor = Ink });
        void Place() { center.Width = Math.Min(620, Math.Max(260, home.Width - 32)); search.Width = center.Width - 48; center.Left = Math.Max(16, (home.Width - center.Width) / 2); center.Top = Math.Max(28, (home.Height - footer.Height - center.Height) / 2); }
        home.Resize += (_, _) => Place(); tab.Controls.Add(home); tab.Home = home; Place(); home.BringToFront();
    }
    internal void OpenSettings() => OpenSettings("Privacy");
    void OpenSettings(string selected)
    {
        using var dialog = new Form { Text = "N0VA · Settings", Size = new Size(880, 740), MinimumSize = new Size(760, 600), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var nav = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 180, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        dialog.Controls.Add(wrap); dialog.Controls.Add(nav);
        var draft = prefs with { };
        void Save() { draft.ProxyEnabled = prefs.ProxyEnabled; draft.ProxyPreset = prefs.ProxyPreset; draft.ProxyAddress = prefs.ProxyAddress; draft.AntiRedirect = prefs.AntiRedirect; draft.BlockAds = prefs.BlockAds; draft.Save(Path.Combine(root, "preferences.json")); prefs.Theme = draft.Theme; prefs.Accent = draft.Accent; prefs.Background = draft.Background; prefs.BackgroundFit = draft.BackgroundFit; prefs.Animations = draft.Animations; prefs.ShowFavorites = draft.ShowFavorites; prefs.ShowClock = draft.ShowClock; prefs.SearchEngine = draft.SearchEngine; prefs.Private = draft.Private; prefs.AllowHttp = draft.AllowHttp; prefs.SleepMinutes = draft.SleepMinutes; ApplyTheme(); Sync(); }
        ComboBox Pick(string title, string[] choices, string value, Action<string> change) {
            wrap.Controls.Add(Caption(title)); var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 }; c.Items.AddRange(choices); c.SelectedItem = value; if (c.SelectedIndex < 0) c.SelectedIndex = 0; c.SelectedIndexChanged += (_, _) => change(c.Text); wrap.Controls.Add(c); return c;
        }
        void Toggle(string text, bool value, Action<bool> change) { var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 10, 0, 8) }; c.CheckedChanged += (_, _) => change(c.Checked); wrap.Controls.Add(c); }
        void Page(string name) {
            while (wrap.Controls.Count > 0) wrap.Controls[0].Dispose();
            wrap.Controls.Add(Caption(name, 26));
            if (name == "Appearance") {
                Pick("Theme", ["Graphite", "Midnight", "Paper"], draft.Theme, v => draft.Theme = v);
                Pick("Accent", ["Sage", "Clay", "Blue", "Lavender", "Ice"], draft.Accent, v => draft.Accent = v);
                Pick("Background placement", ["Fill", "Fit"], draft.BackgroundFit, v => draft.BackgroundFit = v);
                Toggle("Use my background", draft.Background, v => draft.Background = v);
                wrap.Controls.Add(ActionButton("Choose image…", () => {
                    using var pick = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" }; if (pick.ShowDialog(dialog) != DialogResult.OK) return;
                    try { if (new FileInfo(pick.FileName).Length > 20 * 1024 * 1024) throw new IOException("Choose an image under 20 MB."); using var input = Image.FromFile(pick.FileName); if ((long)input.Width * input.Height > 40000000) throw new IOException("Choose an image under 40 megapixels."); var scale = Math.Min(1d, 2560d / Math.Max(input.Width, input.Height)); using var small = new Bitmap(input, Math.Max(1, (int)(input.Width * scale)), Math.Max(1, (int)(input.Height * scale))); Directory.CreateDirectory(root); small.Save(Path.Combine(root, "background.tmp"), ImageFormat.Jpeg); File.Move(Path.Combine(root, "background.tmp"), Path.Combine(root, "background.jpg"), true); foreach (var tab in tabs.TabPages) if (tab.Home is HomeSurface h) h.Wallpaper = null; wallpaper?.Dispose(); wallpaper = null; draft.Background = true; Page(name); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); }
                }));
                Toggle("Startup animation (click or press a key to skip)", draft.Animations, v => draft.Animations = v);
                Toggle("Show favorites on new tabs", draft.ShowFavorites, v => draft.ShowFavorites = v);
                Toggle("Show date and time when opening a new tab", draft.ShowClock, v => draft.ShowClock = v);
                Pick("Search engine", ["DuckDuckGo", "Brave Search"], draft.SearchEngine, v => draft.SearchEngine = v);
            } else if (name == "Privacy") {
                var block = new CheckBox { Text = "Block ads and trackers with uBlock Origin Lite", Checked = prefs.BlockAds, AutoSize = true, Margin = new Padding(0, 12, 0, 12) }; wrap.Controls.Add(block);
                block.CheckedChanged += async (_, _) => { if (!block.Enabled) return; block.Enabled = false; try { await SetBlocking(block.Checked); draft.BlockAds = prefs.BlockAds; } catch (Exception ex) { block.Checked = prefs.BlockAds; MessageBox.Show(dialog, ex.Message); } finally { block.Enabled = true; } };
                Toggle("Anti-redirect (also available beside Site files)", prefs.AntiRedirect, v => { SetAntiRedirect(v); draft.AntiRedirect = v; });
                wrap.Controls.Add(Caption("Anti-redirect blocks HTTP redirects, automatic page navigation and new windows. It can interrupt sign-in and download chains. Use the blocked-destination button to inspect a link, or turn it off for a site you trust.", 10));
                Toggle("Temporary browsing data (applies after restart)", draft.Private, v => draft.Private = v);
                Toggle("Allow unencrypted HTTP sites", draft.AllowHttp, v => draft.AllowHttp = v);
                wrap.Controls.Add(Caption("Strict tracking prevention, certificate validation and site-permission denial stay enabled. Downloads, favorites and local download history persist in temporary mode. Your public IP is not hidden by temporary browsing.", 10));
            } else if (name == "Connection & IP") {
                wrap.Controls.Add(Caption("Choose a VPN/proxy preset or enter your own server and port. Change the exit location in your VPN app to change its public IP. Saved browser proxy changes apply after restart."));
                wrap.Controls.Add(ActionButton("Open connection controls", OpenConnection));
            } else if (name == "Downloads") {
                wrap.Controls.Add(Caption("Site files lists links and release assets from the current website. Downloads shows your actual transfers and locally saved history. Both are available at the top while browsing."));
                wrap.Controls.Add(ActionButton("Open download history", () => { dialog.Close(); ShowTransfers(); }));
                wrap.Controls.Add(ActionButton("Clear completed history", () => { library.Transfers.RemoveAll(d => d.State is not ("InProgress" or "Paused")); SaveLibrary(); RenderTransfers(); Notice("Download history cleared. Files remain on disk."); }));
                wrap.Controls.Add(Caption("Every download asks where to save; executable files get an additional warning. Files never run automatically."));
            } else if (name == "Performance") {
                Pick("Suspend inactive, silent tabs after", ["1", "5", "15"], draft.SleepMinutes.ToString(), v => draft.SleepMinutes = int.Parse(v));
                wrap.Controls.Add(Caption("Minutes. Tabs with audio and sessions with active downloads stay awake. New tabs use native controls and create no web renderer until you visit a site. This reduces idle work; memory usage still depends on websites and WebView2."));
            } else {
                wrap.Controls.Add(Caption("Why is this such a good browser?", 18));
                wrap.Controls.Add(Caption("The aim is simple: useful controls without clutter. Ad and tracker blocking is on by default, temporary browsing data is the default, and there is no N0VA analytics service. The native interface shares the installed WebView2 runtime and sleeps idle tabs."));
                wrap.Controls.Add(Caption("Those are design choices, not a claim of perfect privacy or best-in-class performance. Microsoft runtime services can contact Microsoft. This is an unsigned, unaudited preview, and public IP privacy needs a working VPN or proxy."));
                wrap.Controls.Add(Caption("N0VA Browser 0.2.0 · WebView2 " + RuntimeVersion() + "\nuBlock Origin Lite " + Blocker.Version + "\nInstall new N0VA releases for app and bundled filter updates.", 10));
                wrap.Controls.Add(ActionButton("Credits", () => { dialog.Close(); OpenCredits(); }));
                wrap.Controls.Add(ActionButton("Project & updates", () => { dialog.Close(); _ = Navigate(AddTab(), "https://github.com/N0VALID/N0VA/releases"); }));
            }
            wrap.Controls.Add(ActionButton("Save preferences", () => { try { Save(); dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
        }
        foreach (var name in new[] { "Privacy", "Appearance", "Connection & IP", "Downloads", "Performance", "About" }) { var b = ActionButton(name, () => Page(name)); b.Width = 145; nav.Controls.Add(b); }
        Page(selected); dialog.ShowDialog(this);
    }
    static string RuntimeVersion() { try { return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { return "not installed"; } }
}
