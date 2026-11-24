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
