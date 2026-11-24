using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace Nova;

// Runs only when explicitly invoked by CI; uses an isolated, disposable data directory.
internal static class Smoke
{
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
                    using var client = await listener.AcceptTcpClientAsync(cancel.Token);
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);
                    while (await reader.ReadLineAsync(cancel.Token) is { Length: > 0 }) { }
                    var html = "<title>N0VA fixture</title><a href='/app.zip'>app</a><a href='/app.zip'>duplicate</a><a download='manual.pdf' href='/get?id=2'>manual</a><a download href='javascript:alert(1)'>bad</a>";
                    var bytes = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(html) + "\r\n\r\n" + html);
                    await stream.WriteAsync(bytes, cancel.Token);
                }
            } catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
        });
        using var window = new BrowserWindow(root, new Preferences { AllowHttp = true });
        window.Shown += async (_, _) => {
            try {
                var t = window.Current!;
                await window.Navigate(t, origin);
                if (!t.Ready) throw new Exception("WebView2 initialization failed");
                var c = t.View!.CoreWebView2;
                var deadline = DateTime.UtcNow.AddSeconds(40);
                while (c.DocumentTitle != "N0VA fixture" && DateTime.UtcNow < deadline) await Task.Delay(100);
                if (c.DocumentTitle != "N0VA fixture") throw new Exception("Fixture did not load");
                if (!window.BlockerReady || !(await c.Profile.GetBrowserExtensionsAsync()).Any(x => x.IsEnabled)) throw new Exception("Blocker not enabled");
                if (c.Profile.PreferredTrackingPreventionLevel != CoreWebView2TrackingPreventionLevel.Strict || c.Settings.IsWebMessageEnabled || c.Settings.AreHostObjectsAllowed || !c.Settings.IsReputationCheckingRequired) throw new Exception("Privacy settings missing");
                var links = await window.ScanFiles(t);
                if (links.Count != 2 || !links.Any(x => x.Name == "manual.pdf")) throw new Exception("Download discovery failed: " + links.Count);
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
                window.AddTab();
                using var screenshot = new Bitmap(window.Width, window.Height); window.DrawToBitmap(screenshot, window.Bounds with { X = 0, Y = 0 });
                screenshot.Save(Path.Combine(AppContext.BaseDirectory, "smoke.png"));
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), "PASS: native window, runtime, verified uBOL extension, strict tracking, download discovery, unsafe navigation rejection, saved settings.\nRuntime: " + CoreWebView2Environment.GetAvailableBrowserVersionString());
                result = 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), ex.ToString()); }
            finally { window.Close(); }
        };
        Application.Run(window);
        cancel.Cancel(); listener.Stop();
        Program.Cleanup(Path.Combine(root, "Sessions"));
        return result;
    }
}
