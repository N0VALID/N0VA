using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Nova;
internal static class ProxySmoke
{
    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "nova-proxy-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var cancel = new CancellationTokenSource();
        var proxy = new TcpListener(IPAddress.Loopback, 0); proxy.Start();
        var direct = new TcpListener(IPAddress.Loopback, 0); direct.Start();
        var proxyAddress = "http://127.0.0.1:" + ((IPEndPoint)proxy.LocalEndpoint).Port;
        var directUrl = "http://127.0.0.1:" + ((IPEndPoint)direct.LocalEndpoint).Port + "/must-not-reach";
        var proxiedRequests = 0; var directRequests = 0;
        _ = Task.Run(async () => {
            try {
                while (!cancel.IsCancellationRequested) {
                    var client = await proxy.AcceptTcpClientAsync(cancel.Token);
                    _ = Task.Run(async () => { using var owner = client; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token); timeout.CancelAfter(10000);
                        try { using var stream = client.GetStream(); using var reader = new StreamReader(stream, leaveOpen: true);
                            var request = await reader.ReadLineAsync(timeout.Token); while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 }) { }
                            if (request?.StartsWith("GET http://route.invalid/") == true) Interlocked.Increment(ref proxiedRequests);
                            const string html = "<title>Proxy fixture</title>";
                            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Type: text/html\r\nContent-Length: " + html.Length + "\r\n\r\n" + html), timeout.Token);
                        } catch (Exception e) when (e is IOException or OperationCanceledException) { }
                    });
                }
            } catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
        });
        _ = Task.Run(async () => { try { using var client = await direct.AcceptTcpClientAsync(cancel.Token); Interlocked.Increment(ref directRequests); } catch (OperationCanceledException) { } });
        var result = 1;
        using var window = new BrowserWindow(root, new Preferences { AllowHttp = true, ProxyEnabled = true, ProxyPreset = 3, ProxyAddress = proxyAddress }) { IntegrationTest = true };
        window.Shown += async (_, _) => {
            try {
                await window.Navigate(window.Current!, "http://route.invalid/");
                var core = window.Current!.View!.CoreWebView2;
                var until = DateTime.UtcNow.AddSeconds(20); while ((core.DocumentTitle != "Proxy fixture" || window.Current!.Loading) && DateTime.UtcNow < until) await Task.Delay(100);
                if (core.DocumentTitle != "Proxy fixture" || proxiedRequests == 0) throw new Exception("Browser did not use the explicit proxy");
                proxy.Stop();
                var completed = new TaskCompletionSource<bool>();
                core.NavigationCompleted += (_, e) => completed.TrySetResult(e.IsSuccess);
                await window.Navigate(window.Current!, directUrl);
                var success = await completed.Task.WaitAsync(TimeSpan.FromSeconds(25));
                if (success || directRequests != 0) throw new Exception("Unavailable proxy leaked a direct loopback request");
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "proxy-result.txt"), "PASS: WebView2 used the configured proxy; unavailable proxy did not fall back or bypass loopback."); result = 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "proxy-result.txt"), ex.ToString()); }
            finally { window.Close(); }
        };
        Application.Run(window); cancel.Cancel(); proxy.Stop(); direct.Stop(); return result;
    }
}
