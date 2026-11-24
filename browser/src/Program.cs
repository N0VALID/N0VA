namespace Nova;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--check-runtime")) { try { Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); return 0; } catch { return 2; } }
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test")) return Smoke.Run();
        using var mutex = new Mutex(true, @"Local\N0VALID.NovaBrowser", out var first);
        if (!first) { MessageBox.Show("N0VA Browser is already open.", "N0VA Browser"); return 0; }
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "N0VA", "Browser");
        Directory.CreateDirectory(root);
        Cleanup(Path.Combine(root, "Sessions"));
        Cleanup(Path.Combine(root, "Blocker"));
        try { using var window = new BrowserWindow(root); Application.Run(window); return 0; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "N0VA Browser could not start"); return 1; }
        finally { Cleanup(Path.Combine(root, "Sessions")); }
    }
    public static void Cleanup(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var dir in Directory.GetDirectories(directory))
            try { Directory.Delete(dir, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Retry next launch after WebView2 exits. */ }
    }
}
