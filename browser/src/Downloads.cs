using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Nova;

internal sealed partial class BrowserWindow
{
    readonly FlowLayoutPanel transfers = new() { Dock = DockStyle.Bottom, Height = 210, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    internal const string ScanScript = """
        (() => {
          const result = [], seen = new Set();
          const pattern = /\.(?:exe|msi|msix|zip|7z|rar|tar|gz|bz2|xz|pdf|epub|dmg|pkg|deb|rpm|apk|iso|img|mp3|mp4|wav|ogg|webm|csv|docx?|xlsx?|pptx?)(?:$)/i;
          for (const a of Array.from(document.querySelectorAll('a[href]')).slice(0, 10000)) {
            let u; try { u = new URL(a.href, document.baseURI); } catch { continue; }
            if (!['https:', 'http:'].includes(u.protocol) || u.username || u.password || u.href.length > 8192) continue;
            if (!a.hasAttribute('download') && !pattern.test(u.pathname)) continue;
            u.hash = ''; if (seen.has(u.href)) continue; seen.add(u.href);
            let name = a.getAttribute('download') || u.pathname.split('/').pop() || 'download';
            try { name = decodeURIComponent(name); } catch {}
            result.push({name: name.slice(0, 240), url: u.href});
            if (result.length >= 100) break;
          }
          return result;
        })()
        """;
    void BuildDownloads()
    {
        var head = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 145, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        head.Controls.Add(Caption("On this page", 20));
        head.Controls.Add(new Label { Text = "Download links found in the page. Check the source before saving.", Width = 345, Height = 44 });
        head.Controls.Add(ActionButton("Refresh links", () => _ = RefreshFiles()));
        downloads.Controls.Add(fileList); downloads.Controls.Add(transfers); downloads.Controls.Add(head);
        transfers.Controls.Add(Caption("Transfers", 15));
    }
    void ToggleDownloads() { downloads.Visible = !downloads.Visible; if (downloads.Visible) _ = RefreshFiles(); }
    internal async Task<List<PageFile>> ScanFiles(SiteTab t)
    {
        if (!t.Ready) return [];
        var json = await t.View!.CoreWebView2.ExecuteScriptAsync(ScanScript);
        if (json.Length > 1200000) return [];
        return Policy.ParseLinks(json, prefs.AllowHttp);
    }
    async Task RefreshFiles()
    {
        var generation = ++scanGeneration; var t = Current;
        fileList.Controls.Clear();
        try {
            var links = t == null ? [] : await ScanFiles(t);
            if (closing || generation != scanGeneration || t != Current) return;
            if (links.Count == 0) fileList.Controls.Add(new Label { Text = "No direct file links found.\n\nSome sites generate downloads only after you press their download button. Those downloads still appear here when started.", Width = 335, Height = 120 });
            foreach (var link in links) {
                var card = new FlowLayoutPanel { Width = 337, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 0, 12) };
                card.Controls.Add(new Label { Text = link.Name, Width = 325, AutoSize = true, MaximumSize = new Size(325, 0), Font = new Font(Font, FontStyle.Bold), UseMnemonic = false });
                var source = new Uri(link.Url);
                card.Controls.Add(new Label { Text = source.IdnHost + (Policy.RiskyFile(link.Name) ? " · executable / active content" : ""), Width = 325, AutoSize = true, MaximumSize = new Size(325, 0), UseMnemonic = false });
                var save = ActionButton("Open / download…", () => {
                    if (Current != t || t?.Ready != true || !Policy.WebUrl(link.Url, prefs.AllowHttp)) return;
                    if (MessageBox.Show(this, "Request this file from:\n\n" + link.Url + "\n\nThe website controls the file contents.", "Download source", MessageBoxButtons.OKCancel) == DialogResult.OK)
                        t.View!.CoreWebView2.Navigate(link.Url);
                }); card.Controls.Add(save); fileList.Controls.Add(card);
            }
        } catch (Exception ex) { if (!closing && generation == scanGeneration) fileList.Controls.Add(Caption("Could not read download links: " + ex.Message)); }
    }
    void DownloadStarting(CoreWebView2DownloadStartingEventArgs e)
    {
        // A native confirmation is required for every download, including automatic website requests.
        e.Handled = true;
        var operation = e.DownloadOperation;
        var name = Policy.SafeFileName(e.ResultFilePath);
        if (!Policy.WebUrl(operation.Uri, prefs.AllowHttp)) { e.Cancel = true; Notice("Download blocked: unsupported or unencrypted source."); return; }
        if (activeDownloads.Count >= 4) { e.Cancel = true; Notice("Four downloads are active. Wait for one to finish."); return; }
        if (Policy.RiskyFile(name) && MessageBox.Show(this, name + " can run code or change your computer.\n\nSource: " + new Uri(operation.Uri).IdnHost + "\nOnly save it if you trust the publisher. N0VA never runs downloads automatically.", "Save an executable file?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) { e.Cancel = true; return; }
        using var dialog = new SaveFileDialog { FileName = name, Title = "Save from " + new Uri(operation.Uri).IdnHost, OverwritePrompt = true, Filter = "All files|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) { e.Cancel = true; return; }
        var savedPath = dialog.FileName;
        e.ResultFilePath = savedPath;
        activeDownloads.Add(operation); downloads.Visible = true;
        var row = new FlowLayoutPanel { Width = 340, Height = 100, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var label = new Label { Width = 330, Height = 42, UseMnemonic = false };
        var cancel = ActionButton("Cancel", () => operation.Cancel()); row.Controls.Add(label); row.Controls.Add(cancel); transfers.Controls.Add(row);
        void Update() {
            if (IsDisposed || row.IsDisposed) return;
            var total = operation.TotalBytesToReceive;
            label.Text = name + "\n" + (operation.State == CoreWebView2DownloadState.InProgress ? $"{operation.BytesReceived / 1048576d:0.0} MB" + (total > 0 ? $" / {total / 1048576d:0.0} MB" : "") : operation.State.ToString());
            if (operation.State != CoreWebView2DownloadState.InProgress) {
                activeDownloads.Remove(operation); cancel.Visible = false;
                if (operation.State == CoreWebView2DownloadState.Interrupted) label.Text += " · " + operation.InterruptReason;
                if (operation.State == CoreWebView2DownloadState.Completed && !row.Controls.OfType<Button>().Any(b => b.Text == "Show in folder")) {
                    // Reinforce Mark-of-the-Web for NTFS files; never clear the runtime's zone marker.
                    try { File.WriteAllText(savedPath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    var folder = Path.GetDirectoryName(savedPath)!;
                    row.Controls.Add(ActionButton("Show in folder", () => Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "\"" + folder + "\"", UseShellExecute = true })));
                }
            }
        }
        operation.BytesReceivedChanged += (_, _) => Update(); operation.StateChanged += (_, _) => Update(); Update();
    }
}
