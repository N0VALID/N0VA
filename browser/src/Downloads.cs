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
    Button? refreshFiles;
    bool siteFilesMode = true;
    internal bool SiteFilesButtonVisible => files.Visible;
    static void ClearControls(Control owner) { while (owner.Controls.Count > 0) owner.Controls[0].Dispose(); }
    internal const string ScanScript = """
        (() => {
          const result = [], seen = new Set();
          const pattern = /\.(?:exe|msi|msix|zip|7z|rar|tar|gz|bz2|xz|pdf|epub|dmg|pkg|deb|rpm|apk|iso|img|mp3|mp4|wav|ogg|webm|csv|docx?|xlsx?|pptx?)(?:$)/i;
          const add = (name, url, kind) => { if (!seen.has(url)) { seen.add(url); result.push({name:name.slice(0,240),url,kind}); } };
          const release = u => u.hostname === 'github.com' && /^\/[^/]+\/[^/]+\/releases(?:\/(?:latest|tag\/[^?#]+))?\/?$/.test(u.pathname);
          const here = new URL(location.href); if (release(here)) add('Release files', here.href, 'Release');
          for (const a of Array.from(document.querySelectorAll('a[href]')).slice(0, 10000)) {
            let u; try { u = new URL(a.href, document.baseURI); } catch { continue; }
            if (!['https:', 'http:'].includes(u.protocol) || u.username || u.password || u.href.length > 8192) continue;
            if (u.hash && u.origin === here.origin && u.pathname === here.pathname) continue;
            const direct = a.hasAttribute('download') || pattern.test(u.pathname);
            const kind = release(u) ? 'Release' : direct ? 'File' : /download/i.test((a.textContent || '') + ' ' + (a.getAttribute('aria-label') || '')) ? 'Page' : '';
            if (!kind) continue;
            u.hash = '';
            let name = direct ? a.getAttribute('download') || u.pathname.split('/').pop() || 'download' : (a.textContent || '').trim() || 'Download page';
            try { name = decodeURIComponent(name); } catch {}
            add(name, u.href, kind); if (result.length >= 100) break;
          }
          return result;
        })()
        """;
    void BuildDownloads()
    {
        var head = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 160, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        head.Controls.Add(panelTitle); head.Controls.Add(panelHint);
        refreshFiles = ActionButton("Refresh links", () => _ = RefreshFiles()); head.Controls.Add(refreshFiles);
        head.Controls.Add(ActionButton("Close", () => downloads.Visible = false));
        downloads.Controls.Add(fileList); downloads.Controls.Add(transfers); downloads.Controls.Add(head);
        transfers.Visible = false;
    }
    void PanelMode(bool site)
    {
        siteFilesMode = site; panelTitle.Text = site ? "Site files" : "Downloads";
        panelHint.Text = site ? "Links found on this page. Release lists load only when you request them." : "Your transfers and saved history. Files never run automatically.";
        refreshFiles!.Visible = site; fileList.Visible = site; transfers.Visible = !site;
        downloads.Visible = true;
    }
    void ToggleDownloads() { if (downloads.Visible && siteFilesMode) { downloads.Visible = false; return; } PanelMode(true); _ = RefreshFiles(); }
    void ShowTransfers() { PanelMode(false); RenderTransfers(); }
    internal async Task<List<PageFile>> ScanFiles(SiteTab t)
    {
        if (!t.Ready) return [];
        var json = await t.View!.CoreWebView2.ExecuteScriptAsync(ScanScript);
        return json.Length > 1200000 ? [] : Policy.ParseLinks(json, prefs.AllowHttp);
    }
    async Task RefreshFiles()
    {
        if (!siteFilesMode) return;
        var generation = ++scanGeneration; var t = Current;
        ClearControls(fileList);
        if (t?.Ready != true) { if (downloads.Visible) downloads.Visible = false; return; }
        try {
            var links = await ScanFiles(t);
            if (closing || generation != scanGeneration || t != Current) return;
            if (links.Count == 0) fileList.Controls.Add(new Label { Text = "No download links found.\n\nPress Refresh after the site adds links. Some websites generate files only after you press their own download button. Those transfers appear under Downloads.", Width = 335, Height = 130 });
            foreach (var link in links) AddFileCard(link, t, generation);
        } catch (Exception ex) { if (!closing && generation == scanGeneration) fileList.Controls.Add(Caption("Could not read download links: " + ex.Message)); }
    }
    void AddFileCard(PageFile link, SiteTab tab, int generation)
    {
        var card = new FlowLayoutPanel { Width = 337, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 8, 0, 12) };
        card.Controls.Add(new Label { Text = link.Name, Width = 325, AutoSize = true, MaximumSize = new Size(325, 0), Font = new Font(Font, FontStyle.Bold), UseMnemonic = false });
        card.Controls.Add(new Label { Text = new Uri(link.Url).IdnHost + (Policy.RiskyFile(link.Name) ? " · executable" : ""), Width = 325, AutoSize = true, MaximumSize = new Size(325, 0), UseMnemonic = false });
        if (ReleaseDiscovery.Target(link.Url) != null) {
            var resolve = ActionButton("Load release files", () => { });
            resolve.Click += async (_, _) => {
                resolve.Enabled = false; resolve.Text = "Loading…";
                try {
                    var assets = await ReleaseDiscovery.Fetch(link.Url, connection);
                    if (closing || generation != scanGeneration || tab != Current || card.IsDisposed) return;
                    if (assets.Count == 0) card.Controls.Add(Caption("No public release assets found."));
                    foreach (var file in assets) AddFileCard(file, tab, generation);
                    resolve.Text = "Release files loaded";
                } catch (Exception ex) { if (!card.IsDisposed) { resolve.Enabled = true; resolve.Text = "Try release files again"; card.Controls.Add(Caption("Could not load assets: " + ex.Message)); } }
            }; card.Controls.Add(resolve);
        }
        card.Controls.Add(ActionButton(link.Kind == "File" ? "Open / download…" : "Open download page", () => {
            if (Current != tab || !Policy.WebUrl(link.Url, prefs.AllowHttp)) return;
            if (MessageBox.Show(this, link.Url + "\n\nOpen this link? The website controls its contents.", "Download source", MessageBoxButtons.OKCancel) == DialogResult.OK) _ = Navigate(tab, link.Url);
        }));
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
