namespace Nova;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--check-runtime")) { try { Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(); return 0; } catch { return 2; } }
        var waitIndex = Array.IndexOf(args, "--wait-for-exit");
        if (waitIndex >= 0 && waitIndex + 1 < args.Length && int.TryParse(args[waitIndex + 1], out var previous) && previous != Environment.ProcessId) {
            try { using var process = System.Diagnostics.Process.GetProcessById(previous); process.WaitForExit(10000); } catch (ArgumentException) { }
        }
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test") || args.Contains("--proxy-smoke-test") || args.Contains("--download-smoke-test")) {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try { return args.Contains("--download-smoke-test") ? DownloadSmoke.Run() : args.Contains("--proxy-smoke-test") ? ProxySmoke.Run() : Smoke.Run(); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), ex.ToString()); return 1; }
        }
        using var mutex = new Mutex(true, @"Local\N0VALID.NovaBrowser", out var first);
        if (!first) { MessageBox.Show("N0VA Browser is already open.", "N0VA Browser"); return 0; }
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "N0VA", "Browser");
        Directory.CreateDirectory(root);
        Cleanup(Path.Combine(root, "Sessions"));
        Cleanup(Path.Combine(root, "Blocker"));
        bool restart = false;
        try { using var window = new BrowserWindow(root); Application.Run(window); restart = window.RestartRequested; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "N0VA Browser could not start"); return 1; }
        finally { Cleanup(Path.Combine(root, "Sessions")); }
        if (restart) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { ArgumentList = { "--wait-for-exit", Environment.ProcessId.ToString() }, UseShellExecute = false });
        return 0;
    }
    public static void Cleanup(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var dir in Directory.GetDirectories(directory))
            try { Directory.Delete(dir, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Retry next launch after WebView2 exits. */ }
    }
}
