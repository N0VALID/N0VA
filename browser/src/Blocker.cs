using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;

namespace Nova;

internal static class Blocker
{
    public const string Version = "2026.1122.1948";
    public const string Hash = "7617e0b164552b280043a84c24dc85178eed274f7941da91b1325d4f2aca535c";
    public static async Task Enable(CoreWebView2Profile profile, string root)
    {
        // Unpack afresh on each process start: cached extracted files are not trusted.
        var directory = Path.Combine(root, "Blocker", Guid.NewGuid().ToString("N"));
        await Task.Run(() => {
            var zip = Path.Combine(AppContext.BaseDirectory, "ubol.zip");
            using (var file = File.OpenRead(zip))
                if (!Convert.ToHexString(SHA256.HashData(file)).Equals(Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("uBlock Origin Lite verification failed. Reinstall N0VA Browser.");
            Directory.CreateDirectory(directory);
            ZipFile.ExtractToDirectory(zip, directory);
        });
        foreach (var installed in await profile.GetBrowserExtensionsAsync()) await installed.RemoveAsync();
        var extension = await profile.AddBrowserExtensionAsync(directory);
        if (!extension.IsEnabled) await extension.EnableAsync(true);
    }
}
