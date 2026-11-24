using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Nova;

internal sealed partial class BrowserWindow
{
    static Button ActionButton(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(110, 36), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 12, 8), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(100, 115, 103); b.Click += (_, _) => action(); return b;
    }
    static Label Caption(string text, float size = 10) => new() { Text = text, AutoSize = true, MaximumSize = new Size(570, 0), Font = new Font("Segoe UI", size), Margin = new Padding(0, 8, 0, 8) };
    void ApplyTheme()
    {
        BackColor = Surface; ForeColor = Ink; toolbar.BackColor = Surface; toolbar.ForeColor = Ink;
        statusBar.BackColor = Surface; statusBar.ForeColor = Ink; downloads.BackColor = Surface; downloads.ForeColor = Ink;
        address.BackColor = Surface; address.ForeColor = Ink;
        foreach (var t in tabs.TabPages.OfType<SiteTab>()) { t.BackColor = Surface; if (t.Home != null) ShowHome(t); }
        tabs.Invalidate();
    }
    void ShowHome(SiteTab tab)
    {
        tab.Home?.Dispose();
        var home = new Panel { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Ink, BackgroundImageLayout = ImageLayout.Zoom };
        if (prefs.Background) {
            try { using var image = Image.FromFile(Path.Combine(root, "background.jpg")); home.BackgroundImage = new Bitmap(image); }
            catch (Exception e) when (e is IOException or ArgumentException or OutOfMemoryException) { }
        }
        home.Disposed += (_, _) => home.BackgroundImage?.Dispose();
        var box = new FlowLayoutPanel { Width = 620, Height = 360, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(28), BackColor = Surface, ForeColor = Ink };
        box.Controls.Add(Caption("N 0 V A", 15));
        box.Controls.Add(Caption("Room to explore.", 38));
        box.Controls.Add(Caption("A quieter corner of the web. Start with an address or a search."));
        var search = new TextBox { Width = 550, Font = new Font("Segoe UI", 14), PlaceholderText = "Search privately with DuckDuckGo", Margin = new Padding(0, 18, 0, 12), AccessibleName = "Search the web" };
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; address.Text = search.Text; await NavigateInput(); } };
        box.Controls.Add(search);
        box.Controls.Add(Caption("HTTPS by default   /   No N0VA telemetry   /   Local preferences", 9));
        box.Controls.Add(ActionButton("Personalize", OpenSettings));
        home.Controls.Add(box);
        void Place() { box.Left = Math.Max(12, (home.ClientSize.Width - box.Width) / 2); box.Top = Math.Max(24, (home.ClientSize.Height - box.Height) / 2 - 35); }
        home.Resize += (_, _) => Place(); tab.Controls.Add(home); tab.Home = home; Place(); home.BringToFront();
    }
    void OpenSettings()
    {
        using var dialog = new Form { Text = "N0VA · Settings", Size = new Size(680, 740), MinimumSize = new Size(640, 600), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(30), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        dialog.Controls.Add(wrap);
        wrap.Controls.Add(Caption("Make yourself at home.", 25));
        wrap.Controls.Add(Caption("Appearance is saved on this PC, including in temporary sessions."));
        ComboBox Pick(string title, string[] items, string selected) {
            wrap.Controls.Add(Caption(title)); var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
            c.Items.AddRange(items); c.SelectedItem = selected; if (c.SelectedIndex < 0) c.SelectedIndex = 0; wrap.Controls.Add(c); return c;
        }
        var theme = Pick("Color theme", ["Graphite", "Paper"], prefs.Theme);
        var accent = Pick("Tab accent", ["Sage", "Clay", "Blue"], prefs.Accent);
        var background = new CheckBox { Text = "Use my saved background image", Checked = prefs.Background, AutoSize = true, Margin = new Padding(0, 16, 0, 4) }; wrap.Controls.Add(background);
        wrap.Controls.Add(ActionButton("Choose image…", () => {
            using var pick = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp", Title = "Choose a local background" };
            if (pick.ShowDialog(dialog) != DialogResult.OK) return;
            try {
                if (new FileInfo(pick.FileName).Length > 20 * 1024 * 1024) throw new IOException("Choose an image under 20 MB.");
                using var image = Image.FromFile(pick.FileName);
                if ((long)image.Width * image.Height > 40000000) throw new IOException("Choose an image under 40 megapixels.");
                var ratio = Math.Min(1d, 2560d / Math.Max(image.Width, image.Height));
                using var scaled = new Bitmap(Math.Max(1, (int)(image.Width * ratio)), Math.Max(1, (int)(image.Height * ratio)));
                using (var g = Graphics.FromImage(scaled)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(image, 0, 0, scaled.Width, scaled.Height); }
                Directory.CreateDirectory(root); var temp = Path.Combine(root, "background.tmp"); scaled.Save(temp, ImageFormat.Jpeg); File.Move(temp, Path.Combine(root, "background.jpg"), true); background.Checked = true;
            } catch (Exception e) { MessageBox.Show(dialog, "Image could not be saved: " + e.Message); }
        }));
        wrap.Controls.Add(Caption("Privacy & memory", 17));
        var temporary = new CheckBox { Text = "Temporary browsing data (applies after restart)", Checked = prefs.Private, AutoSize = true }; wrap.Controls.Add(temporary);
        var http = new CheckBox { Text = "Allow unencrypted HTTP sites", Checked = prefs.AllowHttp, AutoSize = true }; wrap.Controls.Add(http);
        var sleep = Pick("Suspend inactive, silent tabs after", ["1 minute", "5 minutes", "15 minutes"], prefs.SleepMinutes + (prefs.SleepMinutes == 1 ? " minute" : " minutes"));
        wrap.Controls.Add(Caption("Always on: uBlock Origin Lite, strict tracking prevention, certificate validation. Camera, microphone, location, notifications and other site permission requests are denied. Password saving and autofill are off.", 9));
        wrap.Controls.Add(Caption("Temporary data is removed after closing; locked files are retried next launch. Downloads and preferences stay. This does not hide your IP address. Microsoft WebView2 services, including SmartScreen, can contact Microsoft.", 9));
        wrap.Controls.Add(ActionButton("Save preferences", () => {
            try {
                prefs.Theme = theme.Text; prefs.Accent = accent.Text; prefs.Background = background.Checked;
                prefs.Private = temporary.Checked; prefs.AllowHttp = http.Checked; prefs.SleepMinutes = new[] { 1, 5, 15 }[sleep.SelectedIndex];
                prefs.Save(Path.Combine(root, "preferences.json")); ApplyTheme(); dialog.Close(); Sync();
            } catch (Exception e) { MessageBox.Show(dialog, "Could not save settings: " + e.Message); }
        }));
        wrap.Controls.Add(Caption("N0VA Browser 0.1.0 · Windows x64 preview\nBlocker " + Blocker.Version + " · WebView2 " + RuntimeVersion() + "\nThe runtime updates through Microsoft. Install new N0VA releases for app and filter updates. There is no automatic N0VA updater.", 9));
        wrap.Controls.Add(ActionButton("Project & updates", () => { dialog.Close(); _ = Navigate(AddTab(), "https://github.com/N0VALID/N0VA/releases"); }));
        dialog.ShowDialog(this);
    }
    static string RuntimeVersion() { try { return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { return "not installed"; } }
}
