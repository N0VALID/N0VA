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
        var ease = 1 - Math.Pow(1 - Math.Min(1, progress * 1.5), 3);
        var fade = Math.Min(1, progress * 4) * Math.Min(1, (1 - progress) * 5);
        var color = Color.FromArgb((int)(BackColor.R + (ink.R - BackColor.R) * fade), (int)(BackColor.G + (ink.G - BackColor.G) * fade), (int)(BackColor.B + (ink.B - BackColor.B) * fade));
        using var font = new Font("Segoe UI", 54, FontStyle.Regular);
        var box = new Rectangle(0, (int)(Height / 2 - 65 + 18 * (1 - ease)), Width, 110);
        TextRenderer.DrawText(e.Graphics, "N 0 V A", font, box, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var half = (int)(48 * ease); e.Graphics.DrawLine(pen, Width / 2 - half, Height / 2 + 64, Width / 2 + half, Height / 2 + 64);
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
        var b = new SoftButton { Text = text, AutoSize = true, MinimumSize = new Size(100, 34), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 12, 8), Cursor = Cursors.Hand, UseMnemonic = false };
        b.FlatAppearance.BorderColor = Color.FromArgb(100, 115, 103); b.Click += (_, _) => action(); return b;
    }
    static Label Caption(string text, float size = 10) => new() { Text = text, AutoSize = true, MaximumSize = new Size(570, 0), Font = new Font("Segoe UI", size), Margin = new Padding(0, 8, 0, 8), UseMnemonic = false };
    void ApplyTheme()
    {
        SuspendLayout(); BackColor = Surface; ForeColor = Ink; toolbar.BackColor = Surface; toolbar.ForeColor = Ink;
        chrome.BackColor = toolsBar.BackColor = favoriteBar.BackColor = Surface; addressHost.FillColor = InputSurface; searchProvider.ForeColor = Ink; SoftButton.MotionEnabled = prefs.Animations;
        chrome.ForeColor = toolsBar.ForeColor = addressHost.ForeColor = bookmark.ForeColor = Ink;
        statusBar.BackColor = Surface; statusBar.ForeColor = Ink; downloads.BackColor = Surface; downloads.ForeColor = Ink;
        address.BackColor = InputSurface; address.ForeColor = Ink; tabs.Theme(Surface, Ink, Accent);
        foreach (var tab in tabs.TabPages) tab.BackColor = Surface;
        RefreshHomePages(); UpdateFavorites(); ResumeLayout(true);
    }
    void RefreshHomePages() { ShowHome(homePage); }
    internal void PlayIntro() { var intro = new IntroSurface(Surface, Accent); Controls.Add(intro); intro.BringToFront(); intro.Focus(); }
    void UpdateCounters()
    {
        counters.RemoveAll(c => c.IsDisposed);
        foreach (var label in counters) label.Text = $"{blockedRequests:N0} blocked requests\nAds & trackers · this session";
    }
    void ShowHome(SiteTab tab)
    {
        tab.Home?.Dispose();
        if (prefs.Background && wallpaper == null) try { using var image = Image.FromFile(Path.Combine(root, "background.jpg")); wallpaper = new Bitmap(image); } catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException) { }
        var home = new HomeSurface { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Ink, Wallpaper = prefs.Background ? wallpaper : null, FillImage = prefs.BackgroundFit == "Fill" };
        var center = new SoftSurface { Width = 490, Height = 240, Radius = 26, FillColor = Color.FromArgb(210, Surface), ForeColor = Ink };
        var wordmark = new Label { Text = "N 0 V A", Left = 20, Top = 22, Width = 450, Height = 70, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 34), ForeColor = Accent, BackColor = Color.Transparent };
        center.Controls.Add(wordmark);
        var searchBox = new SoftSurface { Left = 24, Top = 110, Width = 442, Height = 46, FillColor = InputSurface, Padding = new Padding(18, 12, 18, 10) };
        var search = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), PlaceholderText = "Search " + prefs.SearchEngine + " or enter address", BackColor = InputSurface, ForeColor = Ink, BorderStyle = BorderStyle.None, AccessibleName = "Search or enter address" };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; address.Text = search.Text; await NavigateInput(); } }; searchBox.Controls.Add(search); center.Controls.Add(searchBox);
        if (prefs.ShowFavorites) {
            var favorites = library.Bookmarks.Take(8).ToArray();
            for (int i = 0; i < favorites.Length; i++) {
                var favorite = favorites[i]; var icon = ReadFavoriteIcon(favorite.Url);
                var button = new SoftButton { Glyph = icon == null ? "Globe" : "", Image = icon, Left = (490 - favorites.Length * 48) / 2 + i * 48, Top = 177, Width = 40, Height = 40, ForeColor = Ink, AccessibleName = favorite.Title, Cursor = Cursors.Hand };
                button.Click += (_, _) => _ = Navigate(tab, favorite.Url); button.Disposed += (_, _) => icon?.Dispose(); favoriteTips.SetToolTip(button, favorite.Title + "\n" + new Uri(favorite.Url).IdnHost); center.Controls.Add(button);
            }
        }
        var credits = ActionButton("Credits", OpenCredits); credits.AutoSize = false; credits.Size = new Size(90, 32);
        var personalize = ActionButton("Personalize", () => OpenSettings("Appearance")); personalize.AutoSize = false; personalize.Size = new Size(100, 32);
        var stats = new SoftSurface { Size = new Size(260, 54), FillColor = Color.FromArgb(175, Surface), Radius = 20 };
        var count = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9), BackColor = Color.Transparent, UseMnemonic = false }; counters.Add(count); stats.Controls.Add(count);
        favoriteTips.SetToolTip(count, "Network requests reported blocked during this session; not a count of unique ads."); UpdateCounters();
        home.Controls.Add(center); home.Controls.Add(credits); home.Controls.Add(personalize); home.Controls.Add(stats);
        if (prefs.ShowClock) home.Controls.Add(new Label { Dock = DockStyle.Top, Height = 48, Text = DateTime.Now.ToString("ddd, MMM d  ·  HH:mm"), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(12), BackColor = Color.Transparent, ForeColor = Ink });
        void Place() { center.Left = (home.Width - center.Width) / 2; center.Top = Math.Max(24, (home.Height - center.Height) / 2 - 28); credits.Location = new Point(20, home.Height - 54); personalize.Location = new Point(home.Width - 120, home.Height - 54); stats.Location = new Point((home.Width - stats.Width) / 2, home.Height - 72); }
        home.Resize += (_, _) => Place(); tab.Controls.Add(home); tab.Home = home; Place(); home.BringToFront();
        if (prefs.Animations && !IntegrationTest && tab == Current) {
            var target = center.Top; center.Top += 12; var timer = new System.Windows.Forms.Timer { Interval = 16 };
            timer.Tick += (_, _) => { if (center.IsDisposed) { timer.Dispose(); return; } center.Top -= Math.Max(1, (center.Top - target) / 3); if (center.Top <= target) { center.Top = target; timer.Dispose(); } };
            center.Disposed += (_, _) => timer.Dispose(); timer.Start();
        }
    }

    internal void OpenSettings() => OpenSettings("Privacy");
    void OpenSettings(string selected)
    {
        using var dialog = new Form { Text = "N0VA · Settings", Size = new Size(880, 740), MinimumSize = new Size(760, 600), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var nav = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 180, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        dialog.Controls.Add(wrap); dialog.Controls.Add(nav);
        var draft = prefs with { };
        void Save() { draft.ProxyEnabled = prefs.ProxyEnabled; draft.ProxyPreset = prefs.ProxyPreset; draft.ProxyAddress = prefs.ProxyAddress; draft.AntiRedirect = prefs.AntiRedirect; draft.BlockAds = prefs.BlockAds; draft.Save(Path.Combine(root, "preferences.json")); prefs.Theme = draft.Theme; prefs.Accent = draft.Accent; prefs.Background = draft.Background; prefs.BackgroundFit = draft.BackgroundFit; prefs.Animations = draft.Animations; prefs.ShowFavorites = draft.ShowFavorites; prefs.ShowClock = draft.ShowClock; prefs.SearchEngine = draft.SearchEngine; prefs.Private = draft.Private; prefs.AllowHttp = draft.AllowHttp; prefs.SleepMinutes = draft.SleepMinutes; prefs.CheckUpdates = draft.CheckUpdates; prefs.ToolOrder = draft.ToolOrder ?? []; prefs.HiddenTools = draft.HiddenTools ?? []; LayoutTools(); ApplyTheme(); Sync(); }
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
                    try { if (new FileInfo(pick.FileName).Length > 20 * 1024 * 1024) throw new IOException("Choose an image under 20 MB."); using var input = Image.FromFile(pick.FileName); if ((long)input.Width * input.Height > 40000000) throw new IOException("Choose an image under 40 megapixels."); var scale = Math.Min(1d, 2560d / Math.Max(input.Width, input.Height)); using var small = new Bitmap(input, Math.Max(1, (int)(input.Width * scale)), Math.Max(1, (int)(input.Height * scale))); Directory.CreateDirectory(root); small.Save(Path.Combine(root, "background.tmp"), ImageFormat.Jpeg); File.Move(Path.Combine(root, "background.tmp"), Path.Combine(root, "background.jpg"), true); foreach (var tab in tabs.TabPages.Append(homePage)) if (tab.Home is HomeSurface h) h.Wallpaper = null; wallpaper?.Dispose(); wallpaper = null; draft.Background = true; Page(name); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); }
                }));
                Toggle("Animations and hover effects (startup can be skipped)", draft.Animations, v => draft.Animations = v);
                Toggle("Show favorites on home", draft.ShowFavorites, v => draft.ShowFavorites = v);
                Toggle("Show date and time on home", draft.ShowClock, v => draft.ShowClock = v);
                Pick("Search engine", Policy.SearchEngines, draft.SearchEngine, v => draft.SearchEngine = v);
            } else if (name == "Toolbar") {
                wrap.Controls.Add(Caption("Choose the tools to show. Select a row, then move it up or down to change its order. The menu always stays available."));
                var order = (draft.ToolOrder ?? []).Where(DefaultTools.Contains).Concat(DefaultTools).Distinct().ToArray();
                var list = new CheckedListBox { Width = 420, Height = 170, CheckOnClick = true, BackColor = Surface, ForeColor = Ink, BorderStyle = BorderStyle.None };
                foreach (var item in order) list.Items.Add(item, !(draft.HiddenTools ?? []).Contains(item));
                list.ItemCheck += (_, e) => { var hidden = (draft.HiddenTools ?? []).ToHashSet(); var item = list.Items[e.Index].ToString()!; if (e.NewValue == CheckState.Checked) hidden.Remove(item); else hidden.Add(item); draft.HiddenTools = hidden.ToArray(); };
                wrap.Controls.Add(list);
                void Move(int direction) { var i = list.SelectedIndex; var j = i + direction; if (i < 0 || j < 0 || j >= list.Items.Count) return; var items = list.Items.Cast<string>().ToArray(); (items[i], items[j]) = (items[j], items[i]); draft.ToolOrder = items; Page(name); wrap.Controls.OfType<CheckedListBox>().Single().SelectedIndex = j; }
                var moves = new FlowLayoutPanel { Width = 420, Height = 54 }; moves.Controls.Add(ActionButton("Move up", () => Move(-1))); moves.Controls.Add(ActionButton("Move down", () => Move(1))); wrap.Controls.Add(moves);
                wrap.Controls.Add(ActionButton("Restore all tools", () => { draft.ToolOrder = DefaultTools.ToArray(); draft.HiddenTools = []; Page(name); }));
            } else if (name == "Privacy") {
                var block = new CheckBox { Text = "Block ads and trackers with uBlock Origin Lite", Checked = prefs.BlockAds, AutoSize = true, Margin = new Padding(0, 12, 0, 12) }; wrap.Controls.Add(block);
                block.CheckedChanged += async (_, _) => { if (!block.Enabled) return; block.Enabled = false; try { await SetBlocking(block.Checked); draft.BlockAds = prefs.BlockAds; } catch (Exception ex) { block.Checked = prefs.BlockAds; MessageBox.Show(dialog, ex.Message); } finally { block.Enabled = true; } };
                Toggle("Anti-redirect (also available beside Site files)", prefs.AntiRedirect, v => { SetAntiRedirect(v); draft.AntiRedirect = v; });
                wrap.Controls.Add(Caption("Anti-redirect allows normal links, server redirect chains and same-site navigation. It stops unsolicited cross-site page changes. A file you select in Site files gets one short download handoff. Use its menu to inspect a blocked destination.", 10));
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
                wrap.Controls.Add(Caption("N0VA Browser 0.4.1 · WebView2 " + RuntimeVersion() + "\nuBlock Origin Lite " + Blocker.Version + "\nInstall new N0VA releases for app and bundled filter updates.", 10));
                wrap.Controls.Add(ActionButton("Credits", () => { dialog.Close(); OpenCredits(); }));
                Toggle("Check for app updates when N0VA opens", draft.CheckUpdates, v => draft.CheckUpdates = v);
                wrap.Controls.Add(Caption("Checks GitHub through your current connection. You can choose No when an update is offered; installers never run automatically.", 10));
                wrap.Controls.Add(ActionButton("Check now", () => { dialog.Close(); _ = CheckForUpdate(true); }));
                wrap.Controls.Add(ActionButton("Project & updates", () => { dialog.Close(); _ = Navigate(AddTab(), "https://github.com/N0VALID/N0VA/releases"); }));
            }
            wrap.Controls.Add(ActionButton("Save preferences", () => { try { Save(); dialog.Close(); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } }));
        }
        foreach (var name in new[] { "Privacy", "Appearance", "Toolbar", "Connection & IP", "Downloads", "Performance", "About" }) { var b = ActionButton(name, () => Page(name)); b.Width = 145; nav.Controls.Add(b); }
        Page(selected); dialog.ShowDialog(this);
    }
    static string RuntimeVersion() { try { return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { return "not installed"; } }
}
