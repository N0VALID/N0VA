using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Nova;

internal sealed class SiteTab : SmoothPanel
{
    public WebView2? View;
    public Image? Favicon;
    public Panel? Home;
    public Task? Initialization;
    public string Pending = "";
    public DateTime LastActive = DateTime.UtcNow;
    public bool Ready;
    public bool Loading;
    public bool Suspended;
    public readonly RedirectGuard Guard = new();
    public string? BlockedNavigation;
    public CoreWebView2DevToolsProtocolEventReceiver? NetworkEvents;
    public SiteTab() { Text = "N0VA"; }
}

internal sealed partial class BrowserWindow : Form
{
    readonly string root;
    readonly Preferences prefs;
    readonly Library library;
    readonly bool privateSession;
    readonly string profileDirectory;
    readonly TabDeck tabs = new() { Dock = DockStyle.Fill };
    readonly ToolStrip toolbar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(10, 9, 10, 9), RenderMode = ToolStripRenderMode.System };
    readonly TextBox address = new() { Dock = DockStyle.Fill, AccessibleName = "Search or enter address", PlaceholderText = "Search or enter address", BorderStyle = BorderStyle.None };
    readonly SiteTab homePage = new() { Text = "N0VA" };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripButton back, forward, reload, files, antiRedirect;
    readonly SoftButton bookmark = new() { Glyph = "Star", Width = 32, AccessibleName = "Bookmark page", TabStop = true };
    readonly string connection;
    readonly StatusStrip statusBar = new();
    readonly Panel downloads = new() { Dock = DockStyle.Right, Width = 390, Visible = false, Padding = new Padding(16) };
    readonly FlowLayoutPanel fileList = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    readonly System.Windows.Forms.Timer idle = new() { Interval = 30000 };
    readonly HashSet<CoreWebView2DownloadOperation> activeDownloads = [];
    Task<CoreWebView2Environment>? environment;
    Task<CoreWebView2BrowserExtension>? blocker;
    Task<string>? preparedBlocker;
    bool blockerReady;
    bool closing;
    int scanGeneration;
    Color Surface => prefs.Theme == "Paper" ? Color.FromArgb(245, 244, 239) : prefs.Theme == "Midnight" ? Color.FromArgb(19, 24, 33) : Color.FromArgb(29, 32, 31);
    Color InputSurface => prefs.Theme == "Paper" ? Color.FromArgb(230, 230, 225) : Color.FromArgb(Surface.R + 12, Surface.G + 12, Surface.B + 12);
    Color Ink => prefs.Theme == "Paper" ? Color.FromArgb(35, 42, 38) : Color.FromArgb(230, 233, 227);
    Color Accent => prefs.Accent switch { "Clay" => Color.FromArgb(206, 143, 111), "Blue" => Color.FromArgb(130, 172, 209), "Lavender" => Color.FromArgb(183, 166, 211), "Ice" => Color.FromArgb(150, 210, 210), _ => Color.FromArgb(163, 190, 150) };
    internal SiteTab? Current => tabs.SelectedTab ?? homePage;
    internal Preferences Preferences => prefs;
    internal bool BlockerReady => blockerReady;
    internal bool IntegrationTest;
    internal string LastNetworkFailure = "No loadingFailed event observed";
    internal int ActiveDownloadCount => activeDownloads.Count;
    internal string ProfileDirectory => profileDirectory;

    public BrowserWindow(string root, Preferences? initial = null)
    {
        this.root = root;
        library = Library.Load(Path.Combine(root, "library.json"));
        prefs = initial ?? Preferences.Load(Path.Combine(root, "preferences.json"));
        privateSession = prefs.Private;
        connection = ConnectionPolicy.Endpoint(prefs);
        profileDirectory = privateSession ? Path.Combine(root, "Sessions", Guid.NewGuid().ToString("N")) : Path.Combine(root, "Profile");
        DoubleBuffered = true; ResizeRedraw = true;
        Text = "N0VA Browser";
        Width = 1280; Height = 850; MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Controls.Add(tabs); Controls.Add(chrome); Controls.Add(statusBar);
        toolbar.Dock = DockStyle.None; toolbar.AutoSize = false; toolbar.Padding = Padding.Empty;
        toolbar.Renderer = new ChromeRenderer(); toolsBar.Renderer = new ChromeRenderer();
        chrome.Controls.Add(toolbar); chrome.Controls.Add(addressHost); chrome.Controls.Add(toolsBar); chrome.Controls.Add(bookmark); chrome.Controls.Add(favoriteBar); favoriteBar.Renderer = new ChromeRenderer();
        addressHost.Controls.Add(address); addressHost.Controls.Add(searchProvider); searchProvider.Click += (_, _) => OpenSearchMenu(); bookmark.Click += (_, _) => BookmarkCurrent();
        Controls.Add(exitFullscreen); exitFullscreen.Click += (_, _) => ToggleFullscreen();
        tabs.SetHome(homePage); statusBar.Items.Add(status);
        back = Nav("‹", "Back (Alt+Left)", () => History(-1));
        forward = Nav("›", "Forward (Alt+Right)", () => History(1));
        reload = Nav("↻", "Reload (Ctrl+R)", ReloadCurrent);
        Nav("⌂", "N0VA home (Ctrl+T)", () => AddTab());
        files = AddTool("Site files", "⇩", "Site files", ToggleDownloads);
        AddTool("Downloads", "↓", "Your downloads (Ctrl+J)", ShowTransfers);
        AddTool("Favorites", "★", "Favorites", OpenFavoriteMenu);
        antiRedirect = AddTool("Anti-redirect", "◇", "Anti-redirect", OpenRedirectMenu);
        AddTool("Connection", "◎", "Connection & IP", OpenConnectionMenu);
        AddTool("Menu", "⋮", "Menu and settings", OpenMainMenu);
        LayoutTools(); ConfigurePopup();
        address.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await NavigateInput(); } };
        tabs.CloseRequested += CloseTab; tabs.NewRequested += () => AddTab();
        tabs.SelectedIndexChanged += (_, _) => {
            if (Current is { } t) { t.LastActive = DateTime.UtcNow; if (t.Suspended) { t.View?.CoreWebView2.Resume(); t.Suspended = false; } }
            scanGeneration++; Sync(); if (DownloadsOpen) _ = RefreshFiles();
        };
        Resize += (_, _) => Fit();
        idle.Tick += async (_, _) => {
            foreach (var t in tabs.TabPages.OfType<SiteTab>().ToArray())
                if (t != Current && t.Ready && !t.Loading && !t.Suspended && activeDownloads.Count == 0 &&
                    !t.View!.CoreWebView2.IsDocumentPlayingAudio && DateTime.UtcNow - t.LastActive > TimeSpan.FromMinutes(Math.Clamp(prefs.SleepMinutes, 1, 60)))
                    try { t.Suspended = await t.View.CoreWebView2.TrySuspendAsync(); } catch (Exception) { /* Closing tabs can race suspension. */ }
        };
        FormClosing += (_, e) => {
            if (!IntegrationTest && activeDownloads.Count > 0 && MessageBox.Show(this, "Downloads are still running. Cancel them and close?", "Close N0VA Browser", MessageBoxButtons.YesNo) != DialogResult.Yes) { e.Cancel = true; return; }
            closing = true; idle.Stop(); foreach (var d in activeDownloads.ToArray()) d.Cancel();
        };
        FormClosed += (_, _) => { idle.Dispose(); fileScanTimer.Dispose(); transferPoll.Dispose(); favoriteTips.Dispose(); downloadPopup.Dispose(); toolsMenu?.Dispose(); wallpaper?.Dispose(); foreach (var t in tabs.TabPages.OfType<SiteTab>()) { t.View?.Dispose(); t.Favicon?.Dispose(); } };
        BuildDownloads(); ApplyTheme(); AddTab(); Fit(); idle.Start();
        Shown += async (_, _) => { if (prefs.Animations && !IntegrationTest) PlayIntro(); if (!IntegrationTest && prefs.CheckUpdates) { await Task.Delay(1800); if (!closing) await CheckForUpdate(false); } };
    }
    internal void SetAntiRedirect(bool enabled) { prefs.AntiRedirect = enabled; prefs.Save(Path.Combine(root, "preferences.json")); Sync(); }
    void ReloadCurrent() { if (Current?.Ready == true) { Current.Guard.Permit(Current.View!.CoreWebView2.Source); Current.View.Reload(); } }
    async void History(int offset) {
        var tab = Current; if (tab?.Ready != true) return;
        try {
            var core = tab.View!.CoreWebView2;
            using var data = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync("Page.getNavigationHistory", "{}"));
            var entries = data.RootElement.GetProperty("entries"); var index = data.RootElement.GetProperty("currentIndex").GetInt32() + offset;
            if (index < 0 || index >= entries.GetArrayLength() || tab.IsDisposed) return;
            var entry = entries[index]; var url = entry.GetProperty("url").GetString()!;
            if (!Policy.WebUrl(url, prefs.AllowHttp)) return;
            tab.Guard.Permit(url);
            await core.CallDevToolsProtocolMethodAsync("Page.navigateToHistoryEntry", JsonSerializer.Serialize(new { entryId = entry.GetProperty("id").GetInt32() }));
        } catch (Exception ex) { Notice(ex.Message); }
    }
    void Notice(string text) { if (!IsDisposed) status.Text = text; }
    void Sync()
    {
        statusBar.Visible = !fullscreen && Current?.Ready == true;
        favoriteTips.SetToolTip(searchProvider, "Search engine: " + prefs.SearchEngine); address.PlaceholderText = "Search " + prefs.SearchEngine + " or enter address";
        var t = Current; var c = t?.Ready == true ? t.View!.CoreWebView2 : null;
        back.Enabled = c?.CanGoBack == true; forward.Enabled = c?.CanGoForward == true; reload.Enabled = c != null;
        if (!address.Focused) address.Text = c?.Source == "about:blank" ? "" : c?.Source ?? t?.Pending ?? "";
        files.Available = c != null && Policy.WebUrl(c.Source, true); bookmark.Enabled = files.Available;
        bookmark.FilledGlyph = c != null && library.Bookmarks.Any(b => b.Url == c.Source); bookmark.Invalidate();
        Text = (c == null ? "N0VA Browser" : c.DocumentTitle + " — N0VA Browser");
        status.Text = (privateSession ? "Temporary session" : "Saved browsing data") + "  ·  " +
            (blockerReady ? (prefs.BlockAds ? "uBlock Origin Lite active" : "Ad blocking off") + " · Strict tracking prevention" : "Protection loads before your first website") +
            (c?.Source.StartsWith("http:") == true ? "  ·  HTTP: connection is not encrypted" : "");
        antiRedirect.Checked = prefs.AntiRedirect; antiRedirect.ToolTipText = "Anti-redirect: " + (prefs.AntiRedirect ? "on" : "off") + (t?.BlockedNavigation != null ? " · blocked destination available" : "");
        antiRedirect.ForeColor = prefs.AntiRedirect ? Accent : Ink;
        status.Text += connection == "" ? " · System connection" : " · Proxy route configured";
        Fit(); tabs.Invalidate();
    }
    internal SiteTab AddTab()
    {
        downloadPopup.Close(); tabs.SelectedIndex = -1; ShowHome(homePage); address.Text = ""; address.Focus(); Sync(); return homePage;
    }
    void CloseTab(SiteTab t) { if (t == homePage) return; if (HasActiveDownload(t)) { Notice("Finish or cancel this tab’s download before closing it."); ShowTransfers(); return; } scanGeneration++; tabs.TabPages.Remove(t); t.View?.Dispose(); t.Favicon?.Dispose(); t.Dispose(); if (tabs.TabCount == 0) AddTab(); Sync(); }
    async Task NavigateInput()
    {
        try { var target = Policy.Destination(address.Text, prefs.SearchEngine); if (target.Length > 0) await Navigate(Current ?? AddTab(), target); }
        catch (Exception ex) { Notice(ex.Message); }
    }
    internal async Task Navigate(SiteTab t, string target, bool downloadHandoff = false)
    {
        if (!Policy.WebUrl(target, prefs.AllowHttp)) { Notice("Navigation blocked. HTTPS is required; HTTP can be enabled in Settings."); return; }
        downloadPopup.Close();
        if (t == homePage) {
            if (tabs.TabCount >= 32) { Notice("Close a website tab before opening another."); return; }
            t = new SiteTab { BackColor = Surface }; tabs.TabPages.Add(t); tabs.SelectedTab = t;
        }
        t.Pending = target;
        try {
            t.Initialization ??= Initialize(t); await t.Initialization.WaitAsync(TimeSpan.FromSeconds(60));
            if (t.IsDisposed || closing) return;
            t.Home?.Dispose(); t.Home = null; t.View!.Visible = true;
            t.BlockedNavigation = null; t.Guard.Permit(t.Pending, downloadHandoff);
            t.View.CoreWebView2.Navigate(t.Pending); t.View.Focus(); Sync();
        } catch (Exception e) {
            if (t.IsDisposed || closing) return;
            t.Initialization = null; t.Ready = false; t.View?.Dispose(); t.View = null;
            if (preparedBlocker?.IsFaulted == true) preparedBlocker = null;
            if (environment?.IsFaulted == true) environment = null; if (blocker?.IsFaulted == true) blocker = null;
            Notice("Website not opened: " + e.GetBaseException().Message);
            if (IntegrationTest) throw;
            MessageBox.Show(this, e.GetBaseException().Message + "\n\nNo website was loaded. Check that Microsoft Edge WebView2 Runtime is installed, then try again.", "Protection could not start");
        }
    }
    async Task Initialize(SiteTab t)
    {
        Notice("Starting browser protection…");
        preparedBlocker ??= Blocker.Prepare(root);
        var extensionPath = await preparedBlocker;
        environment ??= CoreWebView2Environment.CreateAsync(null, profileDirectory, new CoreWebView2EnvironmentOptions {
            AreBrowserExtensionsEnabled = true, EnableTrackingPrevention = true,
            AdditionalBrowserArguments = ConnectionPolicy.Arguments(connection)
        });
        var env = await environment; if (t.IsDisposed || closing) return;
        var view = new WebView2 { Dock = DockStyle.Fill, Visible = true, DefaultBackgroundColor = Surface }; t.View = view; t.Controls.Add(view);
        await view.EnsureCoreWebView2Async(env); if (t.IsDisposed || closing) return;
        var c = view.CoreWebView2;
        view.KeyDown += (_, e) => WebShortcut(e);
        c.FaviconChanged += async (_, _) => await CaptureFavicon(t);
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
            if (t.Guard.Blocks(e.Uri, prefs.AntiRedirect, e.IsRedirected, e.IsUserInitiated, e.NavigationId, c.Source)) {
                e.Cancel = true; t.BlockedNavigation = e.Uri; if (t == Current) Sync(); return;
            }
            t.Loading = true; if (t == Current) { scanGeneration++; lastScan = ""; releaseFiles.Clear(); ClearControls(fileList); } LabelTab(t);
        };
        c.FrameNavigationStarting += (_, e) => {
            if (e.Uri != "about:blank" && e.Uri != "about:srcdoc" && !Policy.WebUrl(e.Uri, prefs.AllowHttp)) e.Cancel = true;
        };
        c.NewWindowRequested += async (_, e) => { e.Handled = true; if (e.IsUserInitiated && Policy.WebUrl(e.Uri, prefs.AllowHttp)) await Navigate(AddTab(), e.Uri); };
        c.DocumentTitleChanged += (_, _) => LabelTab(t); c.SourceChanged += (_, _) => { if (t == Current) Sync(); };
        c.HistoryChanged += (_, _) => { if (t == Current) Sync(); };
        c.NavigationCompleted += async (_, e) => { t.Loading = false; t.Guard.Completed(e.NavigationId); LabelTab(t); if (!e.IsSuccess) { Notice("Page load failed: " + e.WebErrorStatus); if (connection.Length > 0 && e.WebErrorStatus is CoreWebView2WebErrorStatus.CannotConnect or CoreWebView2WebErrorStatus.ServerUnreachable or CoreWebView2WebErrorStatus.Disconnected) BeginInvoke((Action)(() => ShowConnectionError(t))); } if (t == Current && DownloadsOpen) await RefreshFiles(); };
        c.DownloadStarting += (_, e) => DownloadStarting(t, e);
        c.IsDefaultDownloadDialogOpenChanged += (_, _) => { if (c.IsDefaultDownloadDialogOpen) downloadPopup.Close(); };
        c.ProcessFailed += (_, _) => Notice("A browser process stopped. Close this tab and open a new one.");
        blocker ??= Blocker.Enable(c.Profile, extensionPath, prefs.BlockAds); await blocker;
        blockerReady = true; t.Ready = true;
        await c.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        var seenBlocks = new HashSet<string>();
        t.NetworkEvents = c.GetDevToolsProtocolEventReceiver("Network.loadingFailed");
        t.NetworkEvents.DevToolsProtocolEventReceived += (_, e) => {
            try { using var data = JsonDocument.Parse(e.ParameterObjectAsJson); var item = data.RootElement; if (IntegrationTest) LastNetworkFailure = e.ParameterObjectAsJson;
                if (item.TryGetProperty("errorText", out var error) && (error.GetString()?.StartsWith("net::ERR_BLOCKED_BY_CLIENT", StringComparison.Ordinal) == true) && item.TryGetProperty("requestId", out var id) && seenBlocks.Add(id.GetString()!)) {
                    blockedRequests++; UpdateCounters(); if (seenBlocks.Count > 5000) seenBlocks.Clear();
                }
            } catch (JsonException) { }
        };
    }
    void LabelTab(SiteTab t)
    {
        if (t.IsDisposed || t.View?.CoreWebView2 == null) return;
        t.Text = (t.Loading ? "… " : "") + t.View.CoreWebView2.DocumentTitle;

        if (t == Current) Sync(); tabs.Invalidate();
    }
}
