using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Nova;

internal sealed partial class BrowserWindow
{
    readonly FlowLayoutPanel transfers = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    readonly HashSet<CoreWebView2DownloadOperation> pausedDownloads = [];
    readonly Dictionary<Transfer, CoreWebView2DownloadOperation> transferOperations = [];
    readonly Dictionary<Transfer, (Label Text, Button Pause, Button Cancel)> transferControls = [];
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
    internal void ShowTransfers() { PanelMode(false); RenderTransfers(); }
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
            var row = new FlowLayoutPanel { Width = 340, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 8, 0, 12) };
            var label = new Label { Width = 330, Height = 55, UseMnemonic = false, Text = item.Name + "\n" + item.State + " · " + item.Started.ToLocalTime().ToString("g") }; row.Controls.Add(label);
            var actions = new FlowLayoutPanel { Width = 330, Height = 48 };
            var pause = ActionButton("Pause", () => { if (transferOperations.TryGetValue(item, out var op)) { if (pausedDownloads.Contains(op) || op.CanResume) { pausedDownloads.Remove(op); op.Resume(); } else { pausedDownloads.Add(op); op.Pause(); } UpdateTransfer(item, op); } });
            var cancel = ActionButton("Cancel", () => { if (transferOperations.TryGetValue(item, out var op)) { pausedDownloads.Remove(op); activeDownloads.Remove(op); op.Cancel(); UpdateTransfer(item, op); SaveLibrary(); } });
            pause.Visible = cancel.Visible = false; actions.Controls.Add(pause); actions.Controls.Add(cancel);
            var folder = ActionButton("Show in folder", () => { var directory = Path.GetDirectoryName(item.Path); if (Directory.Exists(directory)) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "\"" + directory + "\"", UseShellExecute = true }); else Notice("That download folder no longer exists."); }); actions.Controls.Add(folder);
            row.Controls.Add(actions); transferControls[item] = (label, pause, cancel); transfers.Controls.Add(row);
            if (transferOperations.TryGetValue(item, out var operation)) UpdateTransfer(item, operation);
        }
    }
    void UpdateTransfer(Transfer record, CoreWebView2DownloadOperation operation)
    {
        record.State = pausedDownloads.Contains(operation) ? "Paused" : operation.State.ToString();
        if (transferControls.TryGetValue(record, out var controls) && !controls.Text.IsDisposed) {
            var total = operation.TotalBytesToReceive;
            controls.Text.Text = record.Name + "\n" + record.State + (operation.State == CoreWebView2DownloadState.InProgress ? $" · {operation.BytesReceived / 1048576d:0.0} MB" + (total > 0 ? $" / {total / 1048576d:0.0} MB" : "") : "");
            controls.Pause.Visible = operation.State == CoreWebView2DownloadState.InProgress || operation.CanResume;
            controls.Pause.Text = pausedDownloads.Contains(operation) || operation.State == CoreWebView2DownloadState.Interrupted ? "Resume" : "Pause";
            controls.Cancel.Visible = operation.State == CoreWebView2DownloadState.InProgress;
        }
    }
    void DownloadStarting(CoreWebView2DownloadStartingEventArgs e)
    {
        e.Handled = true; var deferral = e.GetDeferral();
        try { BeginInvoke((Action)(() => { try { if (closing) e.Cancel = true; else ConfirmDownload(e); } catch (Exception ex) { e.Cancel = true; Notice("Download canceled: " + ex.Message); } finally { deferral.Complete(); } })); }
        catch { e.Cancel = true; deferral.Complete(); }
    }
    internal Func<string, string, string?>? TestDownloadPicker;
    string? ChooseDownloadPath(string name, string source)
    {
        if (TestDownloadPicker != null) return TestDownloadPicker(name, source);
        using var dialog = new SaveFileDialog { FileName = name, Title = "Save from " + source, OverwritePrompt = true, Filter = "All files|*.*" };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }
    void ConfirmDownload(CoreWebView2DownloadStartingEventArgs e)
    {
        e.Handled = true; var operation = e.DownloadOperation; var name = Policy.SafeFileName(e.ResultFilePath);
        if (!Policy.WebUrl(operation.Uri, prefs.AllowHttp)) { e.Cancel = true; Notice("Download blocked: unsupported or unencrypted source."); return; }
        if (activeDownloads.Count >= 4) { e.Cancel = true; Notice("Four downloads are active. Wait for one to finish."); return; }
        if (Policy.RiskyFile(name) && MessageBox.Show(this, name + " can run code or change your computer.\n\nSource: " + new Uri(operation.Uri).IdnHost + "\nOnly save it if you trust the publisher.", "Save executable file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) { e.Cancel = true; return; }
        var savedPath = ChooseDownloadPath(name, new Uri(operation.Uri).IdnHost); if (savedPath == null) { e.Cancel = true; return; }
        e.ResultFilePath = savedPath; activeDownloads.Add(operation);
        var record = new Transfer { Name = name, Path = savedPath, Source = new Uri(operation.Uri).GetLeftPart(UriPartial.Authority), State = "InProgress" };
        library.Transfers.Insert(0, record); while (library.Transfers.Count > 100) library.Transfers.RemoveAt(library.Transfers.Count - 1);
        transferOperations[record] = operation; SaveLibrary(); ShowTransfers();
        var last = DateTime.MinValue;
        operation.BytesReceivedChanged += (_, _) => { if (DateTime.UtcNow - last > TimeSpan.FromMilliseconds(150) && !closing) { last = DateTime.UtcNow; UpdateTransfer(record, operation); } };
        operation.StateChanged += (_, _) => {
            if (IsDisposed) return;
            if (operation.State == CoreWebView2DownloadState.InProgress || pausedDownloads.Contains(operation)) activeDownloads.Add(operation); else activeDownloads.Remove(operation);
            if (operation.State == CoreWebView2DownloadState.Completed) {
                try { File.WriteAllText(savedPath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            UpdateTransfer(record, operation); SaveLibrary();
        };
        UpdateTransfer(record, operation);
    }
}
