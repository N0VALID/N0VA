using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Nova;

internal sealed class SiteTab : TabPage
{
    public WebView2? View;
    public Panel? Home;
    public Task? Initialization;
    public string Pending = "";
    public DateTime LastActive = DateTime.UtcNow;
    public bool Ready;
    public bool Loading;
    public bool Suspended;
    public SiteTab() : base("New tab") { }
}

internal sealed partial class BrowserWindow : Form
{
    readonly string root;
    readonly Preferences prefs;
    readonly bool privateSession;
    readonly string profileDirectory;
    readonly TabControl tabs = new() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, ItemSize = new Size(200, 35), SizeMode = TabSizeMode.Fixed, ShowToolTips = true };
    readonly ToolStrip toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(10, 9, 10, 9), RenderMode = ToolStripRenderMode.System };
    readonly ToolStripTextBox address = new() { AutoSize = false, AccessibleName = "Search or enter address", ToolTipText = "Search with DuckDuckGo or enter an HTTPS address (Ctrl+L)", BorderStyle = BorderStyle.FixedSingle };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripButton back, forward, reload, files;
    readonly StatusStrip statusBar = new();
    readonly Panel downloads = new() { Dock = DockStyle.Right, Width = 390, Visible = false, Padding = new Padding(16) };
    readonly FlowLayoutPanel fileList = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    readonly System.Windows.Forms.Timer idle = new() { Interval = 30000 };
    readonly HashSet<CoreWebView2DownloadOperation> activeDownloads = [];
    Task<CoreWebView2Environment>? environment;
    Task? blocker;
    bool blockerReady;
    bool closing;
    int scanGeneration;
    Color Surface => prefs.Theme == "Paper" ? Color.FromArgb(245, 244, 239) : Color.FromArgb(29, 32, 31);
    Color Ink => prefs.Theme == "Paper" ? Color.FromArgb(35, 42, 38) : Color.FromArgb(230, 233, 227);
    Color Accent => prefs.Accent switch { "Clay" => Color.FromArgb(206, 143, 111), "Blue" => Color.FromArgb(130, 172, 209), _ => Color.FromArgb(163, 190, 150) };
    internal SiteTab? Current => tabs.SelectedTab as SiteTab;
    internal Preferences Preferences => prefs;
    internal bool BlockerReady => blockerReady;
    internal string ProfileDirectory => profileDirectory;

    public BrowserWindow(string root, Preferences? initial = null)
    {
        this.root = root;
        prefs = initial ?? Preferences.Load(Path.Combine(root, "preferences.json"));
        privateSession = prefs.Private;
        profileDirectory = privateSession ? Path.Combine(root, "Sessions", Guid.NewGuid().ToString("N")) : Path.Combine(root, "Profile");
        Text = "N0VA Browser";
        Width = 1280; Height = 850; MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(tabs); Controls.Add(downloads); Controls.Add(toolbar); Controls.Add(statusBar);
        statusBar.Items.Add(status);
        back = Tool("‹", "Back (Alt+Left)", () => Current?.View?.GoBack());
        forward = Tool("›", "Forward (Alt+Right)", () => Current?.View?.GoForward());
        reload = Tool("↻", "Reload (Ctrl+R)", () => { if (Current?.Ready == true) Current.View!.Reload(); });
        Tool("Home", "Open a fresh start page", () => AddTab());
        toolbar.Items.Add(address);
        Tool("+", "New tab (Ctrl+T)", () => AddTab());
        files = Tool("Downloads", "Files linked on this page and download activity (Ctrl+J)", () => ToggleDownloads());
        Tool("Settings", "Personalization and privacy", OpenSettings);
        address.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await NavigateInput(); } };
        tabs.DrawItem += DrawTab;
        tabs.SelectedIndexChanged += (_, _) => {
            if (Current is { } t) { t.LastActive = DateTime.UtcNow; if (t.Suspended) { t.View?.CoreWebView2.Resume(); t.Suspended = false; } }
            scanGeneration++; Sync(); if (downloads.Visible) _ = RefreshFiles();
        };
        tabs.MouseUp += (_, e) => {
            for (int i = 0; i < tabs.TabCount; i++) {
                var r = tabs.GetTabRect(i);
                if (r.Contains(e.Location) && (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left && e.X > r.Right - 28)) { CloseTab((SiteTab)tabs.TabPages[i]); break; }
            }
        };
        Resize += (_, _) => Fit();
        idle.Tick += async (_, _) => {
            foreach (var t in tabs.TabPages.OfType<SiteTab>().ToArray())
                if (t != Current && t.Ready && !t.Loading && !t.Suspended && activeDownloads.Count == 0 &&
                    !t.View!.CoreWebView2.IsDocumentPlayingAudio && DateTime.UtcNow - t.LastActive > TimeSpan.FromMinutes(Math.Clamp(prefs.SleepMinutes, 1, 60)))
                    try { t.Suspended = await t.View.CoreWebView2.TrySuspendAsync(); } catch (Exception) { /* Closing tabs can race suspension. */ }
        };
        FormClosing += (_, e) => {
            if (activeDownloads.Count > 0 && MessageBox.Show(this, "Downloads are still running. Cancel them and close?", "Close N0VA Browser", MessageBoxButtons.YesNo) != DialogResult.Yes) { e.Cancel = true; return; }
            closing = true; idle.Stop(); foreach (var d in activeDownloads.ToArray()) d.Cancel();
        };
        FormClosed += (_, _) => { idle.Dispose(); foreach (var t in tabs.TabPages.OfType<SiteTab>()) t.View?.Dispose(); };
        BuildDownloads(); ApplyTheme(); AddTab(); Fit(); idle.Start();
    }
    ToolStripButton Tool(string text, string tip, Action action)
    {
        var b = new ToolStripButton(text) { ToolTipText = tip, Padding = new Padding(5), AccessibleName = tip };
        b.Click += (_, _) => { try { action(); } catch (Exception e) { Notice(e.Message); } }; toolbar.Items.Add(b); return b;
    }
    void Fit() { address.Width = Math.Max(180, ClientSize.Width - 500); }
    void Notice(string text) { if (!IsDisposed) status.Text = text; }
    void Sync()
    {
        var t = Current; var c = t?.Ready == true ? t.View!.CoreWebView2 : null;
        back.Enabled = c?.CanGoBack == true; forward.Enabled = c?.CanGoForward == true; reload.Enabled = c != null;
        if (!address.Focused) address.Text = c?.Source == "about:blank" ? "" : c?.Source ?? t?.Pending ?? "";
        Text = (c == null ? "N0VA Browser" : c.DocumentTitle + " — N0VA Browser");
        status.Text = (privateSession ? "Temporary session" : "Saved browsing data") + "  ·  " +
            (blockerReady ? "uBlock Origin Lite active · Strict tracking prevention" : "Protection loads before your first website") +
            (c?.Source.StartsWith("http:") == true ? "  ·  HTTP: connection is not encrypted" : "");
        tabs.Invalidate();
    }
    void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index >= tabs.TabCount) return;
        var r = tabs.GetTabRect(e.Index); using var b = new SolidBrush(Surface); e.Graphics.FillRectangle(b, r);
        if (e.Index == tabs.SelectedIndex) { using var pen = new Pen(Accent, 3); e.Graphics.DrawLine(pen, r.Left + 8, r.Bottom - 2, r.Right - 8, r.Bottom - 2); }
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, new Rectangle(r.X + 12, r.Y, r.Width - 42, r.Height), Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics, "×", Font, new Rectangle(r.Right - 27, r.Y, 23, r.Height), Ink, TextFormatFlags.VerticalCenter);
    }
    internal SiteTab AddTab()
    {
        if (tabs.TabCount >= 32) { Notice("32 tabs are open. Close a tab to open another."); return Current!; }
        var t = new SiteTab { BackColor = Surface }; tabs.TabPages.Add(t); tabs.SelectedTab = t; ShowHome(t); address.Focus(); return t;
    }
    void CloseTab(SiteTab t) { scanGeneration++; tabs.TabPages.Remove(t); t.View?.Dispose(); t.Dispose(); if (tabs.TabCount == 0) AddTab(); Sync(); }
    async Task NavigateInput()
    {
        try { var target = Policy.Destination(address.Text); if (target.Length > 0) await Navigate(Current ?? AddTab(), target); }
        catch (Exception ex) { Notice(ex.Message); }
    }
    internal async Task Navigate(SiteTab t, string target)
    {
        if (!Policy.WebUrl(target, prefs.AllowHttp)) { Notice("Navigation blocked. HTTPS is required; HTTP can be enabled in Settings."); return; }
        t.Pending = target;
        try {
            t.Initialization ??= Initialize(t); await t.Initialization;
            if (t.IsDisposed || closing) return;
            t.Home?.Dispose(); t.Home = null; t.View!.Visible = true;
            t.View.CoreWebView2.Navigate(t.Pending); t.View.Focus(); Sync();
        } catch (Exception e) {
            if (t.IsDisposed || closing) return;
            t.Initialization = null; t.Ready = false; t.View?.Dispose(); t.View = null;
            if (environment?.IsFaulted == true) environment = null; if (blocker?.IsFaulted == true) blocker = null;
            Notice("Website not opened: " + e.GetBaseException().Message);
            MessageBox.Show(this, e.GetBaseException().Message + "\n\nNo website was loaded. Check that Microsoft Edge WebView2 Runtime is installed, then try again.", "Protection could not start");
        }
    }
    async Task Initialize(SiteTab t)
    {
        Notice("Starting browser protection…");
        environment ??= CoreWebView2Environment.CreateAsync(null, profileDirectory, new CoreWebView2EnvironmentOptions {
            AreBrowserExtensionsEnabled = true, EnableTrackingPrevention = true,
            AdditionalBrowserArguments = "--force-webrtc-ip-handling-policy=disable_non_proxied_udp"
        });
        var env = await environment; if (t.IsDisposed || closing) return;
        var view = new WebView2 { Dock = DockStyle.Fill, Visible = false, DefaultBackgroundColor = Surface }; t.View = view; t.Controls.Add(view);
        await view.EnsureCoreWebView2Async(env); if (t.IsDisposed || closing) return;
        var c = view.CoreWebView2;
        c.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;
        c.Settings.IsPasswordAutosaveEnabled = false; c.Settings.IsGeneralAutofillEnabled = false;
        c.Settings.AreHostObjectsAllowed = false; c.Settings.IsWebMessageEnabled = false;
        c.Settings.AreDevToolsEnabled = false; c.Settings.AreBrowserAcceleratorKeysEnabled = false;
        c.Settings.IsReputationCheckingRequired = true;
        c.Settings.AreDefaultContextMenusEnabled = false;
        c.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.SavesInProfile = false; };
        c.ServerCertificateErrorDetected += (_, e) => { e.Action = CoreWebView2ServerCertificateErrorAction.Cancel; Notice("Certificate error: connection blocked."); };
        c.LaunchingExternalUriScheme += (_, e) => { e.Cancel = true; Notice("External application links are blocked."); };
        c.NavigationStarting += (_, e) => {
            if (!Policy.WebUrl(e.Uri, prefs.AllowHttp)) { e.Cancel = true; Notice("Unsafe or non-HTTPS navigation blocked."); return; }
            t.Loading = true; if (t == Current) { scanGeneration++; fileList.Controls.Clear(); } LabelTab(t);
        };
        c.FrameNavigationStarting += (_, e) => {
            if (e.Uri != "about:blank" && e.Uri != "about:srcdoc" && !Policy.WebUrl(e.Uri, prefs.AllowHttp)) e.Cancel = true;
        };
        c.NewWindowRequested += async (_, e) => { e.Handled = true; if (e.IsUserInitiated && Policy.WebUrl(e.Uri, prefs.AllowHttp)) await Navigate(AddTab(), e.Uri); };
        c.DocumentTitleChanged += (_, _) => LabelTab(t); c.SourceChanged += (_, _) => { if (t == Current) Sync(); };
        c.HistoryChanged += (_, _) => { if (t == Current) Sync(); };
        c.NavigationCompleted += async (_, e) => { t.Loading = false; LabelTab(t); if (!e.IsSuccess) Notice("Page load failed: " + e.WebErrorStatus); if (t == Current && downloads.Visible) await RefreshFiles(); };
        c.DownloadStarting += (_, e) => DownloadStarting(e);
        c.ProcessFailed += (_, _) => Notice("A browser process stopped. Close this tab and open a new one.");
        blocker ??= Blocker.Enable(c.Profile, root); await blocker;
        blockerReady = true; t.Ready = true;
    }
    void LabelTab(SiteTab t)
    {
        if (t.IsDisposed || t.View?.CoreWebView2 == null) return;
        t.Text = (t.Loading ? "… " : "") + t.View.CoreWebView2.DocumentTitle;
        t.ToolTipText = t.View.CoreWebView2.Source;
        if (t == Current) Sync(); tabs.Invalidate();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys key)
    {
        if (key == (Keys.Control | Keys.L)) { address.Focus(); address.SelectAll(); return true; }
        if (key == (Keys.Control | Keys.T)) { AddTab(); return true; }
        if (key == (Keys.Control | Keys.W) && Current is { } t) { CloseTab(t); return true; }
        if (key == (Keys.Control | Keys.J)) { ToggleDownloads(); return true; }
        if (key == (Keys.Control | Keys.R)) { if (Current?.Ready == true) Current.View!.Reload(); return true; }
        if (key == (Keys.Control | Keys.Tab) && tabs.TabCount > 0) { tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.TabCount; return true; }
        if (key == Keys.Escape && Current?.Ready == true) { Current.View!.CoreWebView2.Stop(); return true; }
        return base.ProcessCmdKey(ref msg, key);
    }
}
