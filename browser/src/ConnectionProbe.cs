using System.Net;
namespace Nova;
internal record RouteCheck(bool Success, string Message);
internal static class ConnectionProbe
{
    public static async Task<RouteCheck> Check(string endpoint, string target = "https://api.ipify.org/")
    {
        try {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, Proxy = new WebProxy(ConnectionPolicy.Validate(endpoint)), UseProxy = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 4096 };
            var ip = (await client.GetStringAsync(target)).Trim();
            return IPAddress.TryParse(ip, out _) ? new(true, "Connection works · public IP " + ip) : new(false, "The server responded, but the IP check was not valid.");
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ArgumentException) {
            return new(false, "Could not reach the internet through this proxy. Start its service and try again.");
        }
    }
}
