using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace Nova;

// Runs only when explicitly invoked by CI; uses an isolated, disposable data directory.
internal static class Smoke
{
    static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nova-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int result = 1;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var origin = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cancel = new CancellationTokenSource();
        _ = Task.Run(async () => {
            try {
                while (!cancel.IsCancellationRequested) {
                    var client = await listener.AcceptTcpClientAsync(cancel.Token);
                    // Chromium may preconnect on one socket and send the actual request
                    // on another. Handle each socket independently.
                    _ = Task.Run(async () => {
                    using var connection = client;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    try {
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    var request = await reader.ReadLineAsync(timeout.Token);
                    while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 }) { }
                    var html = "<title>N0VA fixture</title><link rel='icon' href='/favicon.svg'><a href='/app.zip'>app</a><a href='/app.zip'>duplicate</a><a download='manual.pdf' href='/get?id=2'>manual</a><a download href='javascript:alert(1)'>bad</a>";
                    var path = request?.Split(' ')[1] ?? "/";
                    if (path is "/redirect" or "/file-redirect") {
                        await stream.WriteAsync(Encoding.UTF8.GetBytes("HTTP/1.1 302 Found\r\nLocation: " + origin.Replace("127.0.0.1", "localhost") + (path == "/file-redirect" ? "/app.zip" : "/arrived") + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), timeout.Token); return;
                    }
                    if (path == "/favicon.svg") html = "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><rect width='32' height='32' rx='6' fill='#5295c0'/><text x='8' y='24' fill='white' font-size='24'>N</text></svg>";
                    if (path == "/arrived") html = "<title>Arrived</title>";
                    if (path == "/same") html = "<title>Same origin</title><script>setTimeout(()=>location.href='/arrived',100)</script>";
                    if (path == "/script") html = "<title>Script</title><script>setTimeout(()=>location.href='" + origin.Replace("127.0.0.1", "localhost") + "/arrived',100)</script>";
                    if (path == "/meta") html = "<title>Meta</title><meta http-equiv='refresh' content='0;url=" + origin.Replace("127.0.0.1", "localhost") + "/arrived'>";
                    if (path == "/release") html = "<title>Release fixture</title><a href='https://github.com/rustdesk/rustdesk/releases/latest'>Download RustDesk</a>";
                    var attachment = request?.StartsWith("GET /app.zip ") == true;
                    if (attachment) html = "N0VA download fixture";
                    var contentType = attachment ? "application/octet-stream\r\nContent-Disposition: attachment; filename=app.zip" : path == "/favicon.svg" ? "image/svg+xml" : "text/html";
                    var bytes = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: " + contentType + "\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(html) + "\r\n\r\n" + html);
                    await stream.WriteAsync(bytes, timeout.Token);
                    } catch (OperationCanceledException) { } catch (IOException) { }
                    });
                }
            } catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
        });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), "Starting native window");
        using var window = new BrowserWindow(root, new Preferences { AllowHttp = true });
        window.IntegrationTest = true;
        window.Shown += async (_, _) => {
            try {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), "Initializing WebView2 and blocker");
                if (window.SiteFilesButtonVisible) throw new Exception("Site files must be hidden on home");
                if (window.WebsiteTabCount != 0) throw new Exception("Home must not be a website tab");
                window.SetAntiRedirect(true);
                await window.Navigate(window.Current!, origin);
                var t = window.Current!;
                if (!t.Ready) throw new Exception("WebView2 initialization failed");
                var c = t.View!.CoreWebView2;
                var deadline = DateTime.UtcNow.AddSeconds(40);
                while (c.DocumentTitle != "N0VA fixture" && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (c.DocumentTitle != "N0VA fixture") throw new Exception("Fixture did not load. URL: " + c.Source + "; title: " + c.DocumentTitle);
                if (!window.BlockerReady || !(await c.Profile.GetBrowserExtensionsAsync()).Any(x => x.IsEnabled && x.Name.Contains("uBlock", StringComparison.OrdinalIgnoreCase))) throw new Exception("Blocker not enabled");
                if (c.Profile.PreferredTrackingPreventionLevel != CoreWebView2TrackingPreventionLevel.Strict || c.Settings.IsWebMessageEnabled || c.Settings.AreHostObjectsAllowed || !c.Settings.IsReputationCheckingRequired) throw new Exception("Privacy settings missing");
                var links = await window.ScanFiles(t);
                if (links.Count != 2 || !links.Any(x => x.Name == "manual.pdf")) throw new Exception("Download discovery failed: " + links.Count);
                window.Activate(); t.View.Focus(); await Task.Delay(150);
                SendKeys.SendWait("{F11}"); await Task.Delay(250);
                if (!window.IsFullscreen || window.FormBorderStyle != FormBorderStyle.None || window.WebAcceleratorCount == 0) throw new Exception("F11 did not reach the browser from focused web content");
                SendKeys.SendWait("{F11}"); await Task.Delay(200);
                if (window.IsFullscreen) throw new Exception("F11 did not restore the window");
                SendKeys.SendWait("^t"); await Task.Delay(200);
                if (window.Current == t || window.WebsiteTabCount != 1) throw new Exception("Ctrl+T failed from web content");
                window.HandleShortcut(Keys.Control | Keys.W);
                if (window.Current != t) throw new Exception("Ctrl+W on home did not return to website");
                await window.Navigate(window.AddTab(), origin + "/extra"); var extra = window.Current!;
                deadline = DateTime.UtcNow.AddSeconds(10); while (extra.Loading && DateTime.UtcNow < deadline) await Task.Delay(100);
                extra.View!.Focus(); await Task.Delay(100); SendKeys.SendWait("^w"); await Task.Delay(200);
                if (!extra.IsDisposed || window.WebsiteTabCount != 1 || window.Current != t) throw new Exception("Ctrl+W failed from web content");
                window.InvokeTool("Site files");
                deadline = DateTime.UtcNow.AddSeconds(5); while (window.DiscoveredFileCount < 2 && DateTime.UtcNow < deadline) await Task.Delay(100);
                await c.ExecuteScriptAsync("setTimeout(()=>{const a=document.createElement('a');a.href='/late.zip';a.textContent='late.zip';document.body.append(a); const host=document.createElement('div'); document.body.append(host);host.attachShadow({mode:'open'}).innerHTML='<button data-download-url=\"/shadow.zip\">Download</button>';},200)");
                deadline = DateTime.UtcNow.AddSeconds(8); while (window.DiscoveredFileCount < 4 && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (window.DiscoveredFileCount < 4) throw new Exception("Site files did not update automatically for late/shadow links");
                using (var popupShot = new Bitmap(window.DownloadPanel.Width, window.DownloadPanel.Height)) { window.DownloadPanel.DrawToBitmap(popupShot, new Rectangle(Point.Empty, popupShot.Size)); popupShot.Save(Path.Combine(AppContext.BaseDirectory, "downloads.png")); }
                window.InvokeTool("Site files");
                window.BookmarkCurrent();
                deadline = DateTime.UtcNow.AddSeconds(8); while ((!Directory.Exists(Path.Combine(root, "Favicons")) || !Directory.GetFiles(Path.Combine(root, "Favicons"), "*.png").Any()) && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (window.FavoriteIconCount != 1 || !Directory.Exists(Path.Combine(root, "Favicons"))) throw new Exception("Favorite icon was not cached");
                await window.Navigate(t, origin + "/arrived"); await Task.Delay(200); window.OpenFavoriteIcon(0);
                deadline = DateTime.UtcNow.AddSeconds(10); while (c.DocumentTitle != "N0VA fixture" && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (c.DocumentTitle != "N0VA fixture") throw new Exception("Favorite icon did not open its page");
                if (Library.Load(Path.Combine(root, "library.json")).Bookmarks.Count != 1) throw new Exception("Favorite did not persist");
                await window.SetBlocking(false);
                if ((await c.Profile.GetBrowserExtensionsAsync()).Any(x => x.IsEnabled && x.Name.Contains("uBlock"))) throw new Exception("Blocker toggle did not disable");
                await window.SetBlocking(true);
                if (!(await c.Profile.GetBrowserExtensionsAsync()).Any(x => x.IsEnabled && x.Name.Contains("uBlock"))) throw new Exception("Blocker toggle did not enable");
                window.SetAntiRedirect(true);
                foreach (var path in new[] { "/redirect", "/same" }) {
                    await window.Navigate(t, origin + path);
                    var expected = (path == "/redirect" ? origin.Replace("127.0.0.1", "localhost") : origin) + "/arrived";
                    deadline = DateTime.UtcNow.AddSeconds(10);
                    while ((c.Source != expected || c.DocumentTitle != "Arrived" || t.Loading) && DateTime.UtcNow < deadline) await Task.Delay(100);
                    if (t.BlockedNavigation != null || c.Source != expected || c.DocumentTitle != "Arrived") throw new Exception("Legitimate redirect blocked: " + path);
                }
                foreach (var path in new[] { "/script", "/meta" }) {
                    await window.Navigate(t, origin + path);
                    deadline = DateTime.UtcNow.AddSeconds(10);
                    while (t.BlockedNavigation == null && DateTime.UtcNow < deadline) await Task.Delay(100);
                    if (t.BlockedNavigation != origin.Replace("127.0.0.1", "localhost") + "/arrived" || c.DocumentTitle == "Arrived") throw new Exception("Anti-redirect failed for " + path);
                }
                window.SetAntiRedirect(false); await window.Navigate(t, origin + "/script");
                deadline = DateTime.UtcNow.AddSeconds(10);
                while (c.DocumentTitle != "Arrived" && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (c.DocumentTitle != "Arrived") throw new Exception("Redirect toggle off failed");
                window.SetAntiRedirect(true); await window.Navigate(t, origin + "/release");
                deadline = DateTime.UtcNow.AddSeconds(10); while (c.DocumentTitle != "Release fixture" && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!(await window.ScanFiles(t)).Any(x => x.Kind == "Release" && x.Url.Contains("rustdesk/rustdesk"))) throw new Exception("RustDesk release-link pattern not detected");
                await c.ExecuteScriptAsync("const p = new Image(); p.src = 'https://ad.doubleclick.net/nova-block-check.gif'; document.body.append(p);");
                deadline = DateTime.UtcNow.AddSeconds(10); while (window.BlockedRequests == 0 && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (window.BlockedRequests == 0) throw new Exception("Runtime blocked-request counter did not update: " + window.LastNetworkFailure);
                var savedFile = Path.Combine(root, "app.zip");
                bool pickerCalled = false;
                window.TestDownloadPicker = (name, source) => { pickerCalled = name == "app.zip" && source == "localhost"; return savedFile; };
                await window.Navigate(t, origin + "/file-redirect", downloadHandoff: true);
                deadline = DateTime.UtcNow.AddSeconds(20);
                while ((!File.Exists(savedFile) || new FileInfo(savedFile).Length != 21) && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!pickerCalled || !File.Exists(savedFile) || File.ReadAllText(savedFile) != "N0VA download fixture") throw new Exception("Confirmed download did not save correctly");
                deadline = DateTime.UtcNow.AddSeconds(60);
                while (window.ActiveDownloadCount > 0 && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (window.ActiveDownloadCount > 0) throw new Exception("Download did not finish its runtime checks");
                if (!File.ReadAllText(savedFile + ":Zone.Identifier").Contains("ZoneId=3")) throw new Exception("Internet zone marker missing");
                // A canceled save must not create the requested file.
                pickerCalled = false;
                window.TestDownloadPicker = (_, _) => { pickerCalled = true; return null; };
                c.Navigate(origin + "/app.zip");
                deadline = DateTime.UtcNow.AddSeconds(10);
                while (!pickerCalled && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (!pickerCalled) throw new Exception("Download cancellation path not reached");
                c.CloseDefaultDownloadDialog();
                var before = c.Source;
                await window.Navigate(t, "file:///C:/Windows/win.ini");
                if (c.Source != before) throw new Exception("Privileged scheme accepted");
                // Exercise the runtime navigation guard, independent of the address bar validator.
                c.Navigate("file:///C:/Windows/win.ini"); await Task.Delay(500);
                if (c.Source != before) throw new Exception("Runtime file navigation accepted");
                window.Preferences.AllowHttp = false;
                c.Navigate(origin + "/blocked"); await Task.Delay(500);
                if (c.Source != before) throw new Exception("Runtime HTTP navigation accepted");
                window.Preferences.Theme = "Paper"; window.Preferences.Save(Path.Combine(root, "preferences.json"));
                if (Preferences.Load(Path.Combine(root, "preferences.json")).Theme != "Paper") throw new Exception("Settings did not persist");
                if (!Library.Load(Path.Combine(root, "library.json")).Transfers.Any(d => d.Name == "app.zip" && d.State == "Completed")) throw new Exception("Download history did not persist");
                window.Preferences.Theme = "Graphite";
                using (var ui = new System.Windows.Forms.Timer { Interval = 100 }) {
                    ui.Tick += (_, _) => {
                        var settings = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "N0VA · Settings"); if (settings == null) return;
                        ui.Stop();
                        try {
                            var nav = settings.Controls.OfType<FlowLayoutPanel>().Single(p => p.Dock == DockStyle.Left);
                            nav.Controls.OfType<Button>().Single(b => b.Text == "Appearance").PerformClick();
                            var body = settings.Controls.OfType<FlowLayoutPanel>().Single(p => p.Dock == DockStyle.Fill);
                            body.Controls.OfType<ComboBox>().ElementAt(1).SelectedItem = "Blue"; body.Controls.OfType<ComboBox>().Last().SelectedItem = "Google";
                            settings.Refresh(); Application.DoEvents(); Cursor.Position = body.Controls.OfType<Button>().Last().PointToScreen(new Point(15, 15)); Application.DoEvents(); LiveCapture.Save(settings, "settings-live.png");
                            LiveCapture.VerifyButtons(settings);
                            using var settingsShot = new Bitmap(settings.Width, settings.Height); settings.DrawToBitmap(settingsShot, new Rectangle(0, 0, settings.Width, settings.Height)); settingsShot.Save(Path.Combine(AppContext.BaseDirectory, "settings.png"));
                            body.Controls.OfType<Button>().Single(b => b.Text == "Save preferences").PerformClick();
                        } catch (Exception error) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "settings-error.txt"), error.ToString()); settings.Close(); }
                    };
                    ui.Start(); window.OpenSettings();
                }
                if (Preferences.Load(Path.Combine(root, "preferences.json")).SearchEngine != "Google") throw new Exception("Search engine selection did not persist");
                if (Preferences.Load(Path.Combine(root, "preferences.json")).Accent != "Blue") throw new Exception("Settings UI did not save appearance");
                using (var ui = new System.Windows.Forms.Timer { Interval = 100 }) {
                    ui.Tick += (_, _) => {
                        var settings = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "N0VA · Settings"); if (settings == null) return;
                        ui.Stop();
                        try {
                            var nav = settings.Controls.OfType<FlowLayoutPanel>().Single(p => p.Dock == DockStyle.Left);
                            nav.Controls.OfType<Button>().Single(b => b.Text == "Toolbar").PerformClick();
                            var body = settings.Controls.OfType<FlowLayoutPanel>().Single(p => p.Dock == DockStyle.Fill);
                            var list = body.Controls.OfType<CheckedListBox>().Single();
                            list.SetItemChecked(list.Items.IndexOf("Favorites"), false); list.SelectedIndex = list.Items.IndexOf("Connection");
                            body.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b => b.Text == "Move up").PerformClick();
                            body.Controls.OfType<Button>().Single(b => b.Text == "Save preferences").PerformClick();
                        } catch (Exception error) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "settings-error.txt"), error.ToString()); settings.Close(); }
                    }; ui.Start(); window.OpenSettings();
                }
                var toolbarPrefs = Preferences.Load(Path.Combine(root, "preferences.json"));
                if (!toolbarPrefs.HiddenTools.Contains("Favorites") || toolbarPrefs.ToolOrder[3] != "Connection" || window.VisibleToolNames.Contains("Favorites")) throw new Exception("Toolbar customization did not persist/apply");
                window.InvokeTool("Connection");
                if (window.ToolMenu?.Visible != true) throw new Exception("Connection tool did not open its menu");
                Exception? connectionError = null;
                using (var ui = new System.Windows.Forms.Timer { Interval = 100 }) {
                    ui.Tick += (_, _) => {
                        var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "N0VA · Connection & IP"); if (dialog == null) return;
                        ui.Stop();
                        try {
                            var body = dialog.Controls.OfType<FlowLayoutPanel>().Single();
                            body.Controls.OfType<CheckBox>().Single().Checked = true;
                            body.Controls.OfType<ComboBox>().Single().SelectedIndex = 3;
                            body.Controls.OfType<TextBox>().Single().Text = "socks5://127.0.0.1:19050";
                            window.ProbeRoute = _ => Task.FromResult(new RouteCheck(false, "Unavailable test service"));
                            var saveConnection = Descendants(body).OfType<Button>().Single(b => b.Text == "Save connection"); saveConnection.PerformClick();
                            if (Preferences.Load(Path.Combine(root, "preferences.json")).ProxyEnabled) throw new Exception("Unavailable proxy was saved");
                            window.ProbeRoute = _ => Task.FromResult(new RouteCheck(true, "Connection works · test IP 203.0.113.8")); saveConnection.PerformClick();
                            if (!body.Controls.OfType<Label>().Single(l => l.Name == "ConnectionStatus").Text.Contains("Restart N0VA")) throw new Exception("Connection save feedback missing");
                            using var shot = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(shot, new Rectangle(0, 0, dialog.Width, dialog.Height)); shot.Save(Path.Combine(AppContext.BaseDirectory, "connection.png"));
                        } catch (Exception error) { connectionError = error; }
                        dialog.Close();
                    }; ui.Start(); window.ToolMenu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Connection settings…").PerformClick();
                }
                if (connectionError != null) throw connectionError;
                if (Preferences.Load(Path.Combine(root, "preferences.json")).ProxyAddress != "socks5://127.0.0.1:19050") throw new Exception("Connection controls did not save");
                bool declined = false;
                using (var ui = new System.Windows.Forms.Timer { Interval = 100 }) {
                    ui.Tick += (_, _) => {
                        var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == "N0VA · Update available"); if (dialog == null) return;
                        ui.Stop();
                        var actions = dialog.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<FlowLayoutPanel>().Single();
                        actions.Controls.OfType<Button>().Single(b => b.Text == "No").PerformClick(); declined = true;
                    }; ui.Start();
                    if (window.PromptUpdate(new(new Version(9, 0, 0), "https://github.com/N0VALID/N0VA/releases")) || !declined) throw new Exception("Update No option failed");
                }
                window.OpenCredits();
                var linksInCredits = window.Current!.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<LinkLabel>()).Select(l => l.Text).ToArray();
                if (!linksInCredits.Contains("https://github.com/N0VALID/N0VA/") || !linksInCredits.Contains("https://guns.lol/n0valid")) throw new Exception("Credit links missing");
                var widthBefore = window.ViewportWidth; window.ShowTransfers();
                if (!window.DownloadsOpen || widthBefore != window.ViewportWidth) throw new Exception("Download popup resized the page");
                var panel = window.DownloadPanel;
                var header = panel.Controls.OfType<FlowLayoutPanel>().First(p => p.Dock == DockStyle.Top);
                var close = Descendants(header).OfType<Button>().Single(b => b.Text == "Close");
                if (close.Bottom > header.ClientSize.Height) throw new Exception("Download panel Close control is clipped"); close.PerformClick();
                window.AddTab();
                if (window.WebsiteTabCount != 1) throw new Exception("Returning home created a website tab");
                if (window.SiteFilesButtonVisible) throw new Exception("Site files visible on new tab");
                window.WindowState = FormWindowState.Maximized; await Task.Delay(150);
                window.WindowState = FormWindowState.Normal; window.Size = new Size(1100, 760); await Task.Delay(150);
                window.Size = new Size(1280, 850); await Task.Delay(150);
                window.PlayIntro(); await Task.Delay(1400);
                if (window.Controls.OfType<IntroSurface>().Any()) throw new Exception("Intro did not stop");
                using var screenshot = new Bitmap(window.Width, window.Height); window.DrawToBitmap(screenshot, window.Bounds with { X = 0, Y = 0 });
                screenshot.Save(Path.Combine(AppContext.BaseDirectory, "smoke.png"));
                using (var background = new Bitmap(800, 600)) { using var g = Graphics.FromImage(background); using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 800, 600), Color.FromArgb(75, 130, 156), Color.FromArgb(158, 95, 115), 35f); g.FillRectangle(brush, 0, 0, 800, 600); background.Save(Path.Combine(root, "background.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg); }
                window.Preferences.Background = true; window.AddTab(); await Task.Delay(200);
                Cursor.Position = window.PointToScreen(new Point(window.ClientSize.Width - 60, window.ClientSize.Height - 35)); await Task.Delay(250); LiveCapture.Save(window, "home-live.png");
                LiveCapture.VerifyButtons(window);
                using (var backgroundShot = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(backgroundShot, new Rectangle(Point.Empty, backgroundShot.Size)); backgroundShot.Save(Path.Combine(AppContext.BaseDirectory, "background.png")); }
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), "PASS: native window, runtime, verified uBOL extension, strict tracking, download discovery, confirmed/canceled downloads, Internet zone marker, unsafe navigation rejection, redirect compatibility (HTTP/same-site/file chains) and cross-site script/meta blocking, blocker toggle, runtime block counter, favorites/history, release detection, credits, animation and resize, saved settings, toolbar visibility/order, home outside website tabs, popup geometry, Connection failure/save, update No option, native web-focused Ctrl+T/Ctrl+W/F11, cached favicon navigation, live Site files/shadow DOM scanning, Google selection, wallpaper overlays.\nRuntime: " + CoreWebView2Environment.GetAvailableBrowserVersionString());
                result = 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), ex.ToString());
                try { using var shot = new Bitmap(window.Width, window.Height); window.DrawToBitmap(shot, new Rectangle(0, 0, window.Width, window.Height)); shot.Save(Path.Combine(AppContext.BaseDirectory, "smoke.png")); } catch { }
            }
            finally { window.Close(); }
        };
        Application.Run(window);
        cancel.Cancel(); listener.Stop();
        Program.Cleanup(Path.Combine(root, "Sessions"));
        return result;
    }
}
