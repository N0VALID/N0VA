using Nova;

int count = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
foreach (var bad in new[] { "file:///C:/secret", "javascript:alert(1)", "data:text/html,hi", "https://user:password@example.com", "http://example.com", "ms-settings:privacy", "https://" })
    Check(!Policy.WebUrl(bad), "Must block " + bad);
Check(Policy.WebUrl("https://example.com/a.zip"), "HTTPS allowed");
Check(Policy.WebUrl("http://localhost:8000", true), "Explicit HTTP allowed");
Check(Policy.Destination("example.com") == "https://example.com", "HTTPS default");
Check(Policy.Destination("one & two").Contains("one%20%26%20two"), "Search encoded");
try { Policy.Destination("file:///C:/windows"); throw new Exception("privileged destination accepted"); } catch (ArgumentException) { count++; }
Check(Policy.SafeFileName("../../evil.exe") == "evil.exe", "Traversal stripped");
Check(Policy.SafeFileName(@"C:\bad\CON.exe") == "_CON.exe", "Windows device name");
Check(!Policy.SafeFileName("photo\u202Egpj.exe").Contains('\u202E'), "Bidi removed");
Check(Policy.RiskyFile("PAYLOAD.EXE") && Policy.RiskyFile("run.ps1"), "Executable warning");
var links = Policy.ParseLinks("""[{"name":"ok.zip","url":"https://example.com/ok.zip"},{"name":"dupe.zip","url":"https://example.com/ok.zip"},{"name":"bad","url":"javascript:alert(1)"},{"name":"bad","url":"http://example.com/bad.zip"}]""", false);
Check(links.Count == 1, "Untrusted DOM results filtered and deduplicated");
var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
try {
    var file = Path.Combine(root, "settings.json"); var p = Preferences.Load(file);
    Check(p.Private && !p.AllowHttp, "Safe defaults"); p.Theme = "Paper"; p.Background = true; p.Save(file);
    var saved = Preferences.Load(file); Check(saved.Theme == "Paper" && saved.Background, "Persistence");
    File.WriteAllText(file, "broken{"); Check(Preferences.Load(file).Private, "Corruption falls back safely");
} finally { Directory.Delete(root, true); }
Console.WriteLine($"{count} security and persistence checks passed.");
var guard = new RedirectGuard(); guard.Permit("https://example.com/");
Check(!guard.Blocks("https://example.com/", true, false, false, 1), "Explicit browser navigation allowed");
Check(!guard.Blocks("https://cdn.example/file", true, true, false, 1), "Authorized server redirect chain allowed");
Check(guard.Blocks("https://cdn.example/file", true, true, false, 2), "Unrelated redirect chain blocked");
guard.Completed(1);
Check(guard.Blocks("https://elsewhere.example/", true, false, false, 3, "https://example.com/"), "Automatic cross-site navigation blocked");
Check(!guard.Blocks("https://example.com/next", true, false, false, 4, "https://example.com/"), "Same-origin automatic navigation allowed");
Check(!guard.Blocks("https://elsewhere.example/", true, false, true, 5, "https://example.com/"), "Clicked cross-site link allowed");
Check(!guard.Blocks("https://example.com/next", false, true, false), "Redirect toggle off");
guard.Permit("https://example.com/file", true);
Check(!guard.Blocks("https://example.com/file", true, false, false, 6), "Selected file allowed"); guard.Completed(6);
Check(!guard.Blocks("https://cdn.example/file", true, false, false, 7, "https://example.com/"), "One selected-file handoff allowed"); guard.Completed(7);
Check(guard.Blocks("https://ads.example/", true, false, false, 8, "https://cdn.example/"), "Download handoff allowance cannot be reused");
guard.Permit("https://example.com/"); guard.Blocks("https://example.com/", true, false, false, 9);
for (int i = 0; i < 20; i++) Check(!guard.Blocks("https://cdn.example/" + i, true, true, false, 9), "Bounded redirect chain allowed");
Check(guard.Blocks("https://cdn.example/21", true, true, false, 9), "Redirect loop bounded");
foreach (var bad in new[] { "http://a:80 --no-sandbox", "http://a:80;direct://", "socks5://user:pass@host:1080", "http://host:0", "http://host:65536", "http://host:80/path", "file://host:22" }) {
    try { ConnectionPolicy.Validate(bad); throw new Exception("Accepted unsafe proxy " + bad); } catch (ArgumentException) { count++; }
}
Check(ConnectionPolicy.Validate("socks5://[::1]:9050") == "socks5://[::1]:9050", "IPv6 endpoint");
Check(ConnectionPolicy.Endpoint(new Preferences { ProxyEnabled = true }) == "socks5://10.64.0.1:1080", "Mullvad preset");
Check(ConnectionPolicy.Arguments("http://127.0.0.1:8080").Contains("--proxy-bypass-list=<-loopback>"), "No implicit loopback bypass");
Console.WriteLine($"{count} total policy checks passed.");
Check(ReleaseDiscovery.Target("https://github.com/rustdesk/rustdesk/releases/latest")?.Api == "https://api.github.com/repos/rustdesk/rustdesk/releases/latest", "RustDesk release route");
Check(ReleaseDiscovery.Target("https://evil.example/rustdesk/rustdesk/releases/latest") == null, "Release endpoint cannot redirect to arbitrary API");
Check(ReleaseDiscovery.Target("https://github.com/rustdesk/rustdesk/releases/download/v1/app.exe") == null, "Asset URL is not a release endpoint");
var assets = ReleaseDiscovery.Parse("""{"assets":[{"name":"app.exe","browser_download_url":"https://github.com/test/repo/releases/download/v1/app.exe"},{"name":"bad","browser_download_url":"https://elsewhere.example/file.exe"}]}""", "test/repo");
Check(assets.Count == 1 && assets[0].Name == "app.exe", "Release assets are validated");
var libraryRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
try {
    var file = Path.Combine(libraryRoot, "library.json"); var library = new Library();
    library.Bookmarks.Add(new("Example", "https://example.com/")); library.Transfers.Add(new() { Name = "app.zip", State = "InProgress" }); library.Save(file);
    var saved = Library.Load(file); Check(saved.Bookmarks.Count == 1 && saved.Transfers[0].State == "Interrupted", "Library persistence and interrupted transfer recovery");
} finally { Directory.Delete(libraryRoot, true); }
Console.WriteLine($"{count} total checks passed.");
if (args.Contains("--check-rustdesk")) {
    var live = await ReleaseDiscovery.Fetch("https://github.com/rustdesk/rustdesk/releases/latest", "");
    Check(live.Any(x => x.Name.EndsWith(".exe")) && live.Any(x => x.Name.EndsWith(".msi")), "Live RustDesk Windows assets");
    Console.WriteLine($"Live RustDesk check: {live.Count} release assets, Windows EXE and MSI found.");
}

string UpdateJson(string name, string url, bool draft = false) => System.Text.Json.JsonSerializer.Serialize(new[] { new { draft, assets = new[] { new { name, browser_download_url = url } } } });
var updateName = "N0VA-Browser-Setup-9.0.0-x64.exe";
var updateUrl = "https://github.com/N0VALID/N0VA/releases/download/v9/" + updateName;
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl))?.Version == new Version(9, 0, 0), "Newer preview installer offered");
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl, true)) == null, "Draft not offered");
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl.Replace("github.com", "evil.example"))) == null, "Untrusted update host rejected");
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl.Replace("N0VALID", "other"))) == null, "Wrong update repository rejected");
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl + "?redirect=other")) == null, "Update query rejected");
Check(UpdatePolicy.Parse(UpdateJson(updateName, updateUrl.Replace(updateName, "other.exe"))) == null, "Update asset name must match URL");
Check(UpdatePolicy.Parse(UpdateJson(updateName.Replace("9.0.0", UpdatePolicy.Current), updateUrl.Replace("9.0.0", UpdatePolicy.Current))) == null, "Installed version is not offered again");
Check(new Preferences().CheckUpdates && new Preferences().HiddenTools.Length == 0, "New installs check for updates and show every tool");
Console.WriteLine($"{count} total checks passed.");
foreach (var engine in Policy.SearchEngines) {
    var destination = Policy.Destination("one & two", engine);
    Check(destination.Contains("one%20%26%20two"), "Search query encoded for " + engine);
}
Check(Policy.Destination("test words", "Google").StartsWith("https://www.google.com/search?q="), "Google route");
Check(DownloadDiscovery.Official("https://www.fortnite.com/download")?.Url == DownloadDiscovery.EpicInstaller, "Fortnite offers verified Epic launcher route");
Check(DownloadDiscovery.Official("https://store.epicgames.com/en-US/p/fortnite") != null, "Epic store launcher discovery");
Check(DownloadDiscovery.Official("https://fortnite.com.evil.example/download") == null && DownloadDiscovery.Official("http://www.fortnite.com/download") == null, "No official link mapping on spoofed or insecure origins");
var actions = Policy.ParseLinks("""[{"name":"Download PC","url":"https://example.com/","kind":"Action","actionId":"n1"},{"name":"Download Mac","url":"https://example.com/","kind":"Action","actionId":"n2"}]""", false);
Check(actions.Count == 2, "Separate download buttons preserved");
var proxyListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); proxyListener.Start();
var probeEndpoint = "http://127.0.0.1:" + ((System.Net.IPEndPoint)proxyListener.LocalEndpoint).Port;
var serveProbe = Task.Run(async () => {
    using var client = await proxyListener.AcceptTcpClientAsync(); using var stream = client.GetStream(); using var reader = new StreamReader(stream, leaveOpen: true);
    var request = await reader.ReadLineAsync(); while (await reader.ReadLineAsync() is { Length: > 0 }) { }
    var body = "203.0.113.8";
    var bytes = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n" + body); await stream.WriteAsync(bytes);
    return request;
});
var routeCheck = await ConnectionProbe.Check(probeEndpoint, "http://probe.invalid/check");
Check(routeCheck.Success && routeCheck.Message.Contains("203.0.113.8"), "Connection test reads IP through proxy");
Check((await serveProbe)?.StartsWith("GET http://probe.invalid/check ") == true, "Connection test uses selected proxy"); proxyListener.Stop();
Check(!(await ConnectionProbe.Check(probeEndpoint, "http://probe.invalid/check")).Success, "Unavailable proxy test fails");
Console.WriteLine($"{count} total checks passed.");

var receiving = new Transfer { State = "InProgress", BytesReceived = 46 * 1048576, TotalBytes = 46 * 1048576 };
Check(DownloadStatus.Describe(receiving, DateTime.UtcNow).Contains("Waiting for the browser"), "Full byte count must not claim completion");
receiving.State = "Completed";
Check(DownloadStatus.Describe(receiving, DateTime.UtcNow) == "Completed · saved", "Only runtime completion is final");
receiving.State = "Interrupted"; receiving.InterruptReason = "FileSecurityCheckFailed";
Check(DownloadStatus.Describe(receiving, DateTime.UtcNow).Contains("security check"), "Security failure is explained");
receiving.InterruptReason = "FileNoSpace";
Check(DownloadStatus.Describe(receiving, DateTime.UtcNow).Contains("disk space"), "Disk failure is explained");
receiving.State = "Canceled";
Check(DownloadStatus.Describe(receiving, DateTime.UtcNow) == "Canceled", "Canceled state is not generic interrupted");
Console.WriteLine($"{count} total checks passed.");
