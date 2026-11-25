using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Nova;

internal sealed partial class BrowserWindow
{
    readonly FlowLayoutPanel transfers = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    readonly HashSet<CoreWebView2DownloadOperation> pausedDownloads = [];
    readonly Dictionary<Transfer, CoreWebView2DownloadOperation> transferOperations = [];
    readonly Dictionary<Transfer, (Label Text, Button Pause, Button Cancel, Button Folder, Button Details)> transferControls = [];
    readonly Dictionary<Transfer, SiteTab> transferOwners = [];
    readonly System.Windows.Forms.Timer transferPoll = new() { Interval = 500 };
    internal Func<string, bool>? TestExecutableConsent;
    internal bool LastDownloadUsesRuntimeUi;
    internal bool TestSuppressDownloadEvents;
    internal IReadOnlyList<Transfer> DownloadRecords => library.Transfers;
    internal void ReconcileDownloads() { foreach (var (record, operation) in transferOperations.ToArray()) RefreshTransfer(record, operation); }
    internal void DownloadDetails(Transfer record) => OpenDownloadDetails(record);
    readonly Label panelTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 20), Margin = new Padding(0, 8, 0, 8) };
    readonly Label panelHint = new() { Width = 345, Height = 44 };
    bool siteFilesMode = true;
    internal bool SiteFilesButtonVisible => files.Visible;
    static void ClearControls(Control owner) { while (owner.Controls.Count > 0) owner.Controls[0].Dispose(); }
    readonly System.Windows.Forms.Timer fileScanTimer = new() { Interval = 1000 };
    bool scanning;
    string lastScan = "";
    readonly Dictionary<string, List<PageFile>> releaseFiles = [];
    internal int DiscoveredFileCount => fileList.Controls.Count;
    internal const string ScanScript = DownloadDiscovery.Script;
    void BuildDownloads()
    {
        transferPoll.Tick += (_, _) => ReconcileDownloads();
        var head = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 108, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var titleRow = new Panel { Width = 360, Height = 40 };
        panelTitle.Font = new Font("Segoe UI", 18); panelTitle.Location = new Point(0, 3); titleRow.Controls.Add(panelTitle);
        var close = ActionButton("Close", () => downloadPopup.Close()); close.AutoSize = false; close.MinimumSize = Size.Empty; close.SetBounds(288, 0, 68, 34); titleRow.Controls.Add(close);
        head.Controls.Add(titleRow); panelHint.Width = 350; panelHint.Height = 48; head.Controls.Add(panelHint);
        downloads.Controls.Add(fileList); downloads.Controls.Add(transfers); downloads.Controls.Add(head); transfers.Visible = false;
        fileScanTimer.Tick += async (_, _) => { if (DownloadsOpen && siteFilesMode) await RefreshFiles(); else fileScanTimer.Stop(); };
    }
    void PanelMode(bool site)
    {
        siteFilesMode = site; panelTitle.Text = site ? "Site files" : "Downloads";
        panelHint.Text = site ? "Updates automatically while open. Choose a file or a download page." : "Your transfers and saved history.";
        fileList.Visible = site; transfers.Visible = !site; ShowDownloadsPopup();
        if (site) fileScanTimer.Start(); else fileScanTimer.Stop();
    }
    void ToggleDownloads() { if (DownloadsOpen && siteFilesMode) { downloadPopup.Close(); return; } lastScan = ""; PanelMode(true); _ = RefreshFiles(); }
    internal void ShowTransfers() { if (Current?.Ready == true) Current.View!.CoreWebView2.CloseDefaultDownloadDialog(); ReconcileDownloads(); PanelMode(false); RenderTransfers(); }
    internal async Task<List<PageFile>> ScanFiles(SiteTab t)
    {
        if (!t.Ready) return [];
        var json = await t.View!.CoreWebView2.ExecuteScriptAsync(ScanScript);
        var links = json.Length > 1200000 ? new List<PageFile>() : Policy.ParseLinks(json, prefs.AllowHttp);
        var official = DownloadDiscovery.Official(t.View.CoreWebView2.Source);
        if (official != null && links.All(x => x.Url != official.Url)) links.Insert(0, official);
        return links;
    }
    async Task RefreshFiles()
    {
        if (!siteFilesMode || scanning) return;
        var generation = scanGeneration; var t = Current;
        if (t?.Ready != true) { if (DownloadsOpen) downloadPopup.Close(); return; }
        scanning = true;
        try {
            var links = await ScanFiles(t);
            if (closing || generation != scanGeneration || t != Current || !siteFilesMode) return;
            var key = System.Text.Json.JsonSerializer.Serialize(links);
            if (key == lastScan && fileList.Controls.Count > 0) return;
            lastScan = key; var scroll = fileList.VerticalScroll.Value; ClearControls(fileList);
            if (links.Count == 0) fileList.Controls.Add(new Label { Text = "Looking for files and download controls…\n\nThis list updates as the page loads. Some sites create a file only after you use their download button.", Width = 350, Height = 112 });
            foreach (var link in links) { AddFileCard(link, t, generation); if (releaseFiles.TryGetValue(link.Url, out var assets)) foreach (var asset in assets) AddFileCard(asset, t, generation); }
            fileList.AutoScrollPosition = new Point(0, scroll);
        } catch (Exception ex) { if (!closing && generation == scanGeneration) panelHint.Text = "Could not inspect this page: " + ex.Message; }
        finally { scanning = false; }
    }
    void AddFileCard(PageFile link, SiteTab tab, int generation)
    {
        var card = new FlowLayoutPanel { Width = 350, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 2, 0, 10), Margin = new Padding(0, 0, 0, 6) };
        var open = ActionButton(link.Name, () => { }); open.AutoSize = false; open.Width = 346; open.Height = 38; open.Margin = new Padding(0, 4, 0, 3); open.AccessibleName = link.Name;
        open.Click += async (_, _) => {
            if (Current != tab || generation != scanGeneration || !Policy.WebUrl(link.Url, prefs.AllowHttp)) return;
            if (link.Kind == "Action") {
                if (link.ActionId == null || !System.Text.RegularExpressions.Regex.IsMatch(link.ActionId, "^n[a-f0-9]+$")) return;
                downloadPopup.Close();
                var id = System.Text.Json.JsonSerializer.Serialize(link.ActionId);
                await tab.View!.CoreWebView2.ExecuteScriptAsync("(() => { const find = root => { for (const e of root.querySelectorAll('*')) { if (e.dataset?.novaDownload === " + id + ") { e.scrollIntoView({block:'center',behavior:'smooth'}); e.focus(); const old=e.style.outline; e.style.outline='3px solid #7eafd4'; setTimeout(()=>e.style.outline=old,2200); return true; } if (e.shadowRoot && find(e.shadowRoot)) return true; if(e.tagName==='IFRAME') try { if(e.contentDocument && find(e.contentDocument)) return true; } catch{} } }; find(document); })()");
                return;
            }
            if (ReleaseDiscovery.Target(link.Url) != null) {
                open.Enabled = false; open.Text = "Loading release files…";
                try { var assets = await ReleaseDiscovery.Fetch(link.Url, connection); if (closing || generation != scanGeneration || tab != Current) return; releaseFiles[link.Url] = assets; lastScan = ""; await RefreshFiles(); }
                catch (Exception ex) { if (!card.IsDisposed) { open.Enabled = true; open.Text = link.Name; panelHint.Text = "Could not load release files: " + ex.Message; } }
                return;
            }
            await Navigate(tab, link.Url, downloadHandoff: link.Kind == "File");
        };
        card.Controls.Add(open);
        var kind = link.Kind switch { "Action" => "Show button on page", "Release" => "Show release files", "File" => "Download / open", _ => "Download page" };
        card.Controls.Add(new Label { Text = new Uri(link.Url).IdnHost + " · " + kind, Width = 346, AutoSize = true, MaximumSize = new Size(346, 0), Font = new Font(Font.FontFamily, 9), UseMnemonic = false, Margin = Padding.Empty });
        fileList.Controls.Add(card);
    }
    void RenderTransfers()
    {
        transferControls.Clear(); ClearControls(transfers);
        if (library.Transfers.Count == 0) transfers.Controls.Add(Caption("No downloads yet. Your completed files will appear here."));
        foreach (var item in library.Transfers) {
            var row = new FlowLayoutPanel { Width = 350, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 8, 0, 12) };
            var label = new Label { Width = 342, AutoSize = true, MaximumSize = new Size(342, 0), UseMnemonic = false }; row.Controls.Add(label);
            var actions = new FlowLayoutPanel { Width = 342, AutoSize = true, MaximumSize = new Size(342, 0), WrapContents = true };
            var pause = ActionButton("Pause", () => {
                if (!transferOperations.TryGetValue(item, out var op)) return;
                try { if (pausedDownloads.Contains(op) || op.CanResume) { pausedDownloads.Remove(op); op.Resume(); } else { pausedDownloads.Add(op); op.Pause(); } RefreshTransfer(item, op); } catch (Exception ex) { Notice(ex.Message); }
            });
            var cancel = ActionButton("Cancel", () => {
                if (!transferOperations.TryGetValue(item, out var op)) return;
                pausedDownloads.Remove(op); op.Cancel(); RefreshTransfer(item, op);
            });
            var folder = ActionButton("Show in folder", () => {
                var directory = Path.GetDirectoryName(item.Path);
                if (File.Exists(item.Path) && Directory.Exists(directory)) Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", item.Path }, UseShellExecute = true });
                else Notice("The downloaded file is no longer at its saved location.");
            });
            var details = ActionButton("Browser details", () => OpenDownloadDetails(item));
            actions.Controls.Add(pause); actions.Controls.Add(cancel); actions.Controls.Add(details); actions.Controls.Add(folder);
            row.Controls.Add(actions); transferControls[item] = (label, pause, cancel, folder, details); transfers.Controls.Add(row);
            PaintTransfer(item);
        }
    }
    void PaintTransfer(Transfer record)
    {
        if (!transferControls.TryGetValue(record, out var controls) || controls.Text.IsDisposed) return;
        controls.Text.Text = record.Name + "\n" + DownloadStatus.Describe(record, DateTime.UtcNow);
        var live = transferOperations.TryGetValue(record, out var operation) && transferOwners.TryGetValue(record, out var owner) && !owner.IsDisposed;
        controls.Pause.Visible = live && record.State is not ("Completed" or "Canceled") && (record.State is "InProgress" or "Paused" || operation!.CanResume);
        controls.Pause.Text = record.State is "Paused" or "Interrupted" ? "Resume" : "Pause";
        controls.Cancel.Visible = live && record.State is "InProgress" or "Paused";
        controls.Folder.Visible = record.State == "Completed"; controls.Folder.Enabled = File.Exists(record.Path);
        controls.Details.Visible = live && record.State != "Completed";
    }
    void RefreshTransfer(Transfer record, CoreWebView2DownloadOperation operation)
    {
        if (closing || IsDisposed) return;
        if (record.State is "Completed" or "Canceled") { PaintTransfer(record); return; }
        var old = record.State; var oldReason = record.InterruptReason;
        try {
            var state = operation.State;
            var reason = state == CoreWebView2DownloadState.Interrupted ? operation.InterruptReason.ToString() : "";
            if (state == CoreWebView2DownloadState.Completed || state == CoreWebView2DownloadState.Interrupted && reason != "UserPaused") pausedDownloads.Remove(operation);
            record.State = state == CoreWebView2DownloadState.Completed ? "Completed" : reason == "UserCanceled" ? "Canceled" : pausedDownloads.Contains(operation) ? "Paused" : state.ToString();
            record.InterruptReason = reason;
            var received = operation.BytesReceived;
            if (record.BytesReceived != received) record.LastProgress = DateTime.UtcNow;
            record.BytesReceived = received; record.TotalBytes = Convert.ToInt64(operation.TotalBytesToReceive);
            if (record.State is "InProgress" or "Paused") activeDownloads.Add(operation); else activeDownloads.Remove(operation);
            if (record.State == "Completed" && old != "Completed") {
                try { File.WriteAllText(record.Path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        } catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ObjectDisposedException) {
            activeDownloads.Remove(operation); pausedDownloads.Remove(operation); record.State = "Interrupted"; record.InterruptReason = "BrowserClosed";
        }
        PaintTransfer(record);
        if (record.State != old || record.InterruptReason != oldReason) SaveLibrary();
        if (activeDownloads.Count > 0) transferPoll.Start(); else transferPoll.Stop();
    }
    void OpenDownloadDetails(Transfer record)
    {
        if (!transferOwners.TryGetValue(record, out var tab) || tab.IsDisposed || !tab.Ready) { Notice("The original download tab is closed."); return; }
        downloadPopup.Close(); tabs.SelectedTab = tab; tab.View!.Visible = true; tab.Home?.Dispose(); tab.Home = null;
        tab.View.CoreWebView2.OpenDefaultDownloadDialog();
    }
    bool HasActiveDownload(SiteTab tab) => transferOwners.Any(x => x.Value == tab && x.Key.State is "InProgress" or "Paused");
    void DownloadStarting(SiteTab tab, CoreWebView2DownloadStartingEventArgs e)
    {
        // The runtime must retain its UI for SmartScreen decisions and errors.
        // Receiving every byte is not the same as completing the download.
        e.Handled = false; var deferral = e.GetDeferral();
        try { BeginInvoke((Action)(() => { try { if (closing) e.Cancel = true; else ConfirmDownload(tab, e); } catch (Exception ex) { e.Cancel = true; Notice("Download canceled: " + ex.Message); } finally { deferral.Complete(); } })); }
        catch { e.Cancel = true; deferral.Complete(); }
    }
    internal Func<string, string, string?>? TestDownloadPicker;
    string? ChooseDownloadPath(string name, string source)
    {
        if (TestDownloadPicker != null) return TestDownloadPicker(name, source);
        using var dialog = new SaveFileDialog { FileName = name, Title = "Save from " + source, OverwritePrompt = true, Filter = "All files|*.*" };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }
    void ConfirmDownload(SiteTab tab, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Handled = false; LastDownloadUsesRuntimeUi = true; var operation = e.DownloadOperation; var name = Policy.SafeFileName(e.ResultFilePath);
        if (!Policy.WebUrl(operation.Uri, prefs.AllowHttp)) { e.Cancel = true; Notice("Download blocked: unsupported or unencrypted source."); return; }
        if (activeDownloads.Count >= 4) { e.Cancel = true; Notice("Four downloads are active. Wait for one to finish."); return; }
        if (Policy.RiskyFile(name) && !(IntegrationTest && TestExecutableConsent != null ? TestExecutableConsent(name) : MessageBox.Show(this, name + " can run code or change your computer.\n\nSource: " + new Uri(operation.Uri).IdnHost + "\nOnly save it if you trust the publisher.", "Save executable file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)) { e.Cancel = true; return; }
        var savedPath = ChooseDownloadPath(name, new Uri(operation.Uri).IdnHost); if (savedPath == null) { e.Cancel = true; return; }
        e.ResultFilePath = savedPath; activeDownloads.Add(operation);
        var record = new Transfer { Name = name, Path = savedPath, Source = new Uri(operation.Uri).GetLeftPart(UriPartial.Authority), State = "InProgress" };
        library.Transfers.Insert(0, record); while (library.Transfers.Count > 100) library.Transfers.RemoveAt(library.Transfers.Count - 1);
        transferOperations[record] = operation; transferOwners[record] = tab; SaveLibrary();
        var lastRefresh = DateTime.MinValue;
        operation.BytesReceivedChanged += (_, _) => { if (IntegrationTest && TestSuppressDownloadEvents) return; if (DateTime.UtcNow - lastRefresh < TimeSpan.FromMilliseconds(150)) return; lastRefresh = DateTime.UtcNow; RefreshTransfer(record, operation); };
        operation.StateChanged += (_, _) => { if (!(IntegrationTest && TestSuppressDownloadEvents)) RefreshTransfer(record, operation); };
        // Reconcile immediately and periodically too: completion may occur before
        // subscriptions, and not every runtime revision reliably delivers events.
        RefreshTransfer(record, operation);
    }
}
