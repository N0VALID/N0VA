using System.Drawing.Drawing2D;

namespace Nova;

internal sealed class SoftButton : Button
{
    bool hovered;
    public SoftButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return new Size(Math.Max(MinimumSize.Width, size.Width + 28), Math.Max(MinimumSize.Height, size.Height + 14));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = SoftSurface.Shape(new Rectangle(1, 1, Width - 3, Height - 3), 12);
        using var brush = new SolidBrush(hovered ? ControlPaint.Light(BackColor, .12f) : BackColor); e.Graphics.FillPath(brush, path);
        using var pen = new Pen(Color.FromArgb(70, ForeColor)); e.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(8, 2, Width - 16, Height - 4), Enabled ? ForeColor : SystemColors.GrayText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(6, 5, Width - 12, Height - 10));
    }
}
internal sealed class ChromeRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed && (e.Item is not ToolStripButton b || !b.Checked)) return;
        var color = e.Item.Owner?.BackColor ?? Color.FromArgb(30, 32, 31);
        var dark = color.GetBrightness() < .5f;
        using var brush = new SolidBrush(dark ? Color.FromArgb(Math.Min(255, color.R + 24), Math.Min(255, color.G + 24), Math.Min(255, color.B + 24)) : ControlPaint.Dark(color, .06f));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = SoftSurface.Shape(new Rectangle(2, 2, e.Item.Width - 4, e.Item.Height - 4), 10); e.Graphics.FillPath(brush, shape);
    }
}
internal sealed class SoftSurface : SmoothPanel
{
    public static GraphicsPath Shape(Rectangle r, int radius)
    {
        var path = new GraphicsPath(); var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) return path;
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shape(new Rectangle(1, 1, Width - 3, Height - 3), 18); using var pen = new Pen(Color.FromArgb(80, ForeColor)); e.Graphics.DrawPath(pen, path);
    }
}
internal sealed partial class BrowserWindow
{
    readonly SmoothPanel chrome = new() { Dock = DockStyle.Top, Height = 58 };
    readonly SoftSurface addressHost = new() { Height = 38, Padding = new Padding(15, 8, 15, 8) };
    readonly ToolStrip toolsBar = new() { GripStyle = ToolStripGripStyle.Hidden, AutoSize = false, Dock = DockStyle.None, Height = 40, Padding = Padding.Empty, RenderMode = ToolStripRenderMode.System };
    readonly Dictionary<string, ToolStripButton> toolItems = [];
    readonly ToolStripDropDown downloadPopup = new() { Padding = new Padding(1), AutoClose = true };
    ContextMenuStrip? toolsMenu;
    internal bool DownloadsOpen => downloadPopup.Visible;
    internal int WebsiteTabCount => tabs.TabCount;
    internal int ViewportWidth => tabs.Width;
    internal Panel DownloadPanel => downloads;
    internal ContextMenuStrip? ToolMenu => toolsMenu;
    internal void InvokeTool(string name) => toolItems[name].PerformClick();
    internal string[] VisibleToolNames => toolsBar.Items.Cast<ToolStripItem>().Select(i => i.AccessibleName ?? "").ToArray();
    static readonly string[] DefaultTools = ["Site files", "Downloads", "Favorites", "Anti-redirect", "Connection"];
    ToolStripButton Nav(string glyph, string tip, Action action)
    {
        var button = new ToolStripButton(glyph) { AutoSize = false, Width = 34, Height = 34, ToolTipText = tip, AccessibleName = tip, Font = new Font("Segoe UI", 14) };
        button.Click += (_, _) => action(); toolbar.Items.Add(button); return button;
    }
    ToolStripButton AddTool(string name, string glyph, string tip, Action action)
    {
        var button = new ToolStripButton(glyph) { AutoSize = false, Width = 36, Height = 34, ToolTipText = tip, AccessibleName = name, Font = new Font("Segoe UI", 13) };
        button.Click += (_, _) => { try { action(); } catch (Exception ex) { Notice(ex.Message); } }; toolItems[name] = button; return button;
    }
    void LayoutTools()
    {
        toolsBar.SuspendLayout(); toolsBar.Items.Clear();
        var order = (prefs.ToolOrder ?? []).Where(DefaultTools.Contains).Distinct().Concat(DefaultTools).Distinct();
        foreach (var name in order) if (!(prefs.HiddenTools ?? []).Contains(name)) toolsBar.Items.Add(toolItems[name]);
        toolsBar.Items.Add(toolItems["Menu"]); toolsBar.ResumeLayout(); Fit();
    }
    void Fit()
    {
        toolbar.SetBounds(8, 9, 140, 40);
        var rightWidth = toolsBar.Items.Cast<ToolStripItem>().Where(x => x.Available).Sum(x => x.Width + x.Margin.Horizontal) + 8;
        toolsBar.SetBounds(Math.Max(350, chrome.Width - rightWidth - 8), 9, rightWidth, 40);
        var margin = Math.Max(158, rightWidth + 20);
        var width = Math.Min(640, Math.Max(200, chrome.Width - 2 * margin));
        addressHost.SetBounds((chrome.Width - width) / 2, 10, width, 38);
    }
    void ShowMenu(ToolStripItem? anchor, Action<ContextMenuStrip> build)
    {
        toolsMenu?.Dispose(); toolsMenu = new ContextMenuStrip { BackColor = Surface, ForeColor = Ink, ShowImageMargin = false, Font = Font, Padding = new Padding(5) }; build(toolsMenu);
        var location = new Point(chrome.Width - toolsMenu.GetPreferredSize(Size.Empty).Width - 8, chrome.Bottom);
        if (anchor?.Owner != null) location = PointToClient(anchor.Owner.PointToScreen(new Point(anchor.Bounds.Right, anchor.Bounds.Bottom)));
        toolsMenu.Show(this, location, ToolStripDropDownDirection.BelowLeft);
    }
    void OpenMainMenu() => ShowMenu(toolItems["Menu"], menu => {
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Customize toolbar", null, (_, _) => OpenSettings("Toolbar"));
        menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdate(true));
        menu.Items.Add(new ToolStripSeparator());
        foreach (var name in DefaultTools) menu.Items.Add(name, null, (_, _) => toolItems[name].PerformClick());
        if (Current?.BlockedNavigation is { } url) menu.Items.Add("Inspect blocked redirect…", null, async (_, _) => { if (MessageBox.Show(this, url + "\n\nOpen this destination?", "Redirect blocked", MessageBoxButtons.YesNo) == DialogResult.Yes) await Navigate(Current!, url); });
    });
    void OpenFavoriteMenu() => ShowMenu(toolItems["Favorites"], menu => {
        menu.Items.Add("Bookmark this page", null, (_, _) => BookmarkCurrent());
        menu.Items.Add("Manage favorites…", null, (_, _) => OpenFavorites()); menu.Items.Add(new ToolStripSeparator());
        foreach (var favorite in library.Bookmarks.Take(20)) menu.Items.Add(favorite.Title, null, (_, _) => _ = Navigate(AddTab(), favorite.Url));
    });
    void OpenRedirectMenu() => ShowMenu(antiRedirect, menu => {
        var toggle = new ToolStripMenuItem("Anti-redirect") { Checked = prefs.AntiRedirect }; toggle.Click += (_, _) => SetAntiRedirect(!prefs.AntiRedirect); menu.Items.Add(toggle);
        menu.Items.Add(new ToolStripMenuItem("Allows normal links and download redirect chains") { Enabled = false });
        if (Current?.BlockedNavigation is { } url) menu.Items.Add("Inspect blocked destination…", null, async (_, _) => { if (MessageBox.Show(this, url + "\n\nOpen this destination?", "Redirect blocked", MessageBoxButtons.YesNo) == DialogResult.Yes) await Navigate(Current!, url); });
    });
    internal void OpenConnectionMenu() => ShowMenu(toolItems["Connection"], menu => {
        menu.Items.Add(new ToolStripMenuItem(connection == "" ? "Active: Windows connection" : "Active: " + connection) { Enabled = false });
        menu.Items.Add("Connection settings…", null, (_, _) => OpenConnection());
        menu.Items.Add("Check current public IP", null, (_, _) => _ = Navigate(AddTab(), "https://api.ipify.org/"));
    });
    void ShowDownloadsPopup()
    {
        var size = new Size(Math.Min(420, ClientSize.Width - 30), Math.Min(560, ClientSize.Height - 100));
        downloads.Size = size; downloads.Visible = true;
        var host = (ToolStripControlHost)downloadPopup.Items[0]; host.Size = size;
        if (!downloadPopup.Visible) downloadPopup.Show(this, new Point(ClientSize.Width - size.Width - 10, chrome.Bottom), ToolStripDropDownDirection.BelowRight);
    }
    void ConfigurePopup()
    {
        downloads.Dock = DockStyle.None;
        downloadPopup.Items.Add(new ToolStripControlHost(downloads) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = downloads.Size });
        downloadPopup.Closed += (_, _) => { if (Current?.Ready == true) Current.View?.Focus(); };
    }
}
