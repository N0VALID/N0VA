using System.Text.RegularExpressions;

namespace Nova;

internal static class ConnectionPolicy
{
    public static readonly string[] Presets = ["Mullvad (VPN must be connected)", "Tor service (local port 9050)", "Tor Browser (local port 9150)", "Custom proxy"];
    public static string Endpoint(Preferences p) => !p.ProxyEnabled ? "" : p.ProxyPreset switch {
        0 => "socks5://10.64.0.1:1080",
        1 => "socks5://127.0.0.1:9050",
        2 => "socks5://127.0.0.1:9150",
        3 => Validate(p.ProxyAddress),
        _ => throw new ArgumentException("Choose a connection preset.")
    };
    public static string Validate(string value)
    {
        value = (value ?? "").Trim();
        if (!Regex.IsMatch(value, @"\A(?:https?|socks5)://(?:\[[a-fA-F0-9:]+\]|[a-zA-Z0-9.-]+):[0-9]{1,5}/?\z", RegexOptions.IgnoreCase)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Port < 1 || uri.Port > 65535 || uri.UserInfo.Length > 0)
            throw new ArgumentException("Use http://host:port, https://host:port or socks5://host:port, without passwords or paths.");
        return uri.Scheme + "://" + uri.Host + ":" + uri.Port;
    }
    public static string Arguments(string endpoint) => "--force-webrtc-ip-handling-policy=disable_non_proxied_udp" +
        (endpoint == "" ? "" : " --proxy-server=" + Validate(endpoint) + " --proxy-bypass-list=<-loopback> --disable-quic");
}

internal sealed class RedirectGuard
{
    string? permitted;
    public void Permit(string target) { permitted = new Uri(target).AbsoluteUri; }
    public bool Blocks(string target, bool enabled, bool redirected, bool userInitiated)
    {
        var direct = !redirected && Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.AbsoluteUri == permitted;
        permitted = null;
        return enabled && (redirected || !userInitiated && !direct);
    }
}
