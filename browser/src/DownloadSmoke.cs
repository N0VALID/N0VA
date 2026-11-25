using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
namespace Nova;
internal static class DownloadSmoke
{
    static IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Descendants(x)));
    static async Task Until(Func<bool> condition, string failure, int seconds = 30)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition() && DateTime.UtcNow < end) await Task.Delay(100);
        if (!condition()) throw new Exception(failure);
    }
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nova-download-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var fixture = Path.Combine(root, "fixture.zip");
        using (var zip = ZipFile.Open(fixture, ZipArchiveMode.Create)) {
            using var target = zip.CreateEntry("fixture.bin", CompressionLevel.NoCompression).Open();
            var block = new byte[65536]; new Random(42).NextBytes(block);
            for (int i = 0; i < 46 * 16; i++) target.Write(block);
        }
        string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        var expected = Hash(fixture);
        using var cancel = new CancellationTokenSource(); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var origin = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(async () => {
            try {
                while (!cancel.IsCancellationRequested) {
                    var client = await listener.AcceptTcpClientAsync(cancel.Token);
                    _ = Task.Run(async () => {
                        using var owner = client;
                        try {
                            using var stream = client.GetStream(); using var reader = new StreamReader(stream, leaveOpen: true);
                            var request = await reader.ReadLineAsync(cancel.Token); while (await reader.ReadLineAsync(cancel.Token) is { Length: > 0 }) { }
                            var path = request?.Split(' ')[1] ?? "/";
                            if (path is "/large.zip" or "/slow.zip") {
                                var size = new FileInfo(fixture).Length;
                                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Disposition: attachment; filename=" + path[1..] + "\r\nContent-Length: " + size + "\r\nConnection: close\r\n\r\n"), cancel.Token);
                                using var file = File.OpenRead(fixture); var block = new byte[16384]; int read;
                                while ((read = await file.ReadAsync(block, cancel.Token)) > 0) { await stream.WriteAsync(block.AsMemory(0, read), cancel.Token); if (path == "/slow.zip") await Task.Delay(30, cancel.Token); }
                            } else {
                                const string html = "<title>Download regression</title><p>N0VA download regression fixture</p>";
                                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: " + html.Length + "\r\nConnection: close\r\n\r\n" + html), cancel.Token);
                            }
                        } catch (Exception e) when (e is IOException or OperationCanceledException or SocketException) { }
                    });
                }
            } catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
        });
        var report = Path.Combine(AppContext.BaseDirectory, "download-result.txt"); File.WriteAllText(report, "Starting download regression");
        using var window = new BrowserWindow(root, new Preferences { AllowHttp = true, Animations = false }) { IntegrationTest = true };
        int result = 1;
        window.TestDownloadPicker = (name, _) => Path.Combine(root, "saved-" + name);
        window.TestExecutableConsent = _ => true; // Only our own published installer; never execute it.
        window.Shown += async (_, _) => {
            try {
                await window.Navigate(window.Current!, origin); var tab = window.Current!; var core = tab.View!.CoreWebView2;
                await Until(() => core.DocumentTitle == "Download regression", "Fixture did not load");
                window.TestSuppressDownloadEvents = true;
                await window.Navigate(tab, origin + "/large.zip", true);
                await Until(() => window.DownloadRecords.FirstOrDefault()?.State == "Completed", "46 MB ZIP failed to complete via polling", 60);
                var large = window.DownloadRecords[0];
                if (!window.LastDownloadUsesRuntimeUi || window.ActiveDownloadCount != 0 || Hash(large.Path) != expected || !File.ReadAllText(large.Path + ":Zone.Identifier").Contains("ZoneId=3")) throw new Exception("Large-download completion/hash/zone/runtime UI mismatch");
                if (Library.Load(Path.Combine(root, "library.json")).Transfers[0].State != "Completed") throw new Exception("Polled completion did not persist");
                File.AppendAllText(report, "\nPASS: 46 MB ZIP, exact SHA256, completion despite suppressed callbacks, Internet zone, saved history.");
                window.TestSuppressDownloadEvents = false;
                await window.Navigate(tab, origin + "/slow.zip", true);
                await Until(() => window.DownloadRecords[0].Name == "slow.zip" && window.DownloadRecords[0].BytesReceived > 32768, "Slow transfer did not start");
                window.HandleShortcut(Keys.Control | Keys.W);
                if (tab.IsDisposed) throw new Exception("Active download lost its owning tab");
                var slow = window.DownloadRecords[0]; window.ShowTransfers();
                var pause = Descendants(window.DownloadPanel).OfType<Button>().First(b => b.Text == "Pause" && b.Visible); pause.PerformClick();
                await Until(() => slow.State == "Paused", "Pause state did not update");
                pause.PerformClick(); await Until(() => slow.State == "InProgress", "Resume did not update");
                // Exercise changes originating outside our own transfer controls,
                // as when the user pauses/resumes in the native download panel.
                var slowOperation = window.DownloadOperation(slow);
                slowOperation.Pause(); await Until(() => slow.State == "Paused", "External pause did not update");
                slowOperation.Resume(); await Until(() => slow.State == "InProgress", "External resume did not update");
                Descendants(window.DownloadPanel).OfType<Button>().First(b => b.Text == "Cancel" && b.Visible).PerformClick();
                await Until(() => slow.State == "Canceled" && window.ActiveDownloadCount == 0, "Canceled download remained active");
                File.AppendAllText(report, "\nPASS: pause/resume/cancel and active-tab ownership.");
                core.CloseDefaultDownloadDialog();
                const string installer = "https://github.com/N0VALID/N0VA/releases/download/browser-preview-b510bf70/N0VA-Browser-Setup-0.4.0-x64.exe";
                await window.Navigate(tab, installer, true);
                await Until(() => window.DownloadRecords[0].Name.EndsWith(".exe"), "Published installer did not start", 45);
                var actual = window.DownloadRecords[0];
                await Until(() => actual.State is "Completed" or "Interrupted" || actual.TotalBytes > 0 && actual.BytesReceived >= actual.TotalBytes, "Published installer did not transfer", 70);
                window.DownloadDetails(actual); await Task.Delay(800);
                if (!core.IsDefaultDownloadDialogOpen || !core.Settings.IsReputationCheckingRequired) throw new Exception("Runtime download prompts/checks are unavailable");
                if (actual.State == "Completed" && Hash(actual.Path) != "BCD66A0A092293BCB531F16C356085BFB9265DC9F3D38A04D015324632403935") throw new Exception("Published installer hash mismatch");
                LiveCapture.Save(window, "download-live.png");
                File.AppendAllText(report, "\nPASS: exact user-reported installer with runtime UI visible and reputation checks enabled. State: " + actual.State + "; bytes: " + actual.BytesReceived + "/" + actual.TotalBytes + "; reason: " + actual.InterruptReason + ". No installer executed and no warning bypassed.");
                window.ShowTransfers(); await Task.Delay(200); LiveCapture.Save(window, "transfer-live.png");
                result = 0;
            } catch (Exception ex) { File.AppendAllText(report, "\nFAIL: " + ex); try { LiveCapture.Save(window, "download-live.png"); } catch { } }
            finally { window.Close(); }
        };
        Application.Run(window); cancel.Cancel(); listener.Stop(); return result;
    }
}
