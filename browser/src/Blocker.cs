using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;

namespace Nova;

internal static class Blocker
{
    public const string Version = "2026.1122.1948";
    public const string Hash = "7617e0b164552b280043a84c24dc85178eed274f7941da91b1325d4f2aca535c";
    public static Task<string> Prepare(string root) => Task.Run(() => {
        // Verify and rebuild before creating the runtime: saved profiles must never
        // load stale, unverified unpacked extension code during startup.
        var directory = Path.Combine(root, "Blocker", "official-ubol");
        var zip = Path.Combine(AppContext.BaseDirectory, "ubol.zip");
        using (var file = File.OpenRead(zip))
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("uBlock Origin Lite verification failed. Reinstall N0VA Browser.");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);
        ZipFile.ExtractToDirectory(zip, directory);
        return directory;
    });
    public static async Task Enable(CoreWebView2Profile profile, string directory)
    {
        // Adding from the same stable folder reinstalls our extension. Do not
        // remove WebView2's internal extensions (for example its PDF viewer).
        var extension = await profile.AddBrowserExtensionAsync(directory);
        if (!extension.IsEnabled) await extension.EnableAsync(true);
        if (!extension.IsEnabled) throw new InvalidOperationException("uBlock Origin Lite did not enable.");
    }
}
