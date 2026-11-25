using System.Diagnostics;

namespace Nova;

internal sealed partial class BrowserWindow
{
    internal void OpenConnection()
    {
        using var dialog = new Form { Text = "N0VA · Connection & IP", Size = new Size(660, 770), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28), FlowDirection = FlowDirection.TopDown, WrapContents = false }; dialog.Controls.Add(wrap);
        wrap.Controls.Add(Caption("Connection & IP", 24));
        wrap.Controls.Add(Caption(connection == "" ? "Current route: Windows connection / connected VPN" : "Current proxy route: " + connection));
        var enabled = new CheckBox { Text = "Use a proxy route", Checked = prefs.ProxyEnabled, AutoSize = true }; wrap.Controls.Add(enabled);
        var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 540 }; preset.Items.AddRange(ConnectionPolicy.Presets); preset.SelectedIndex = Math.Clamp(prefs.ProxyPreset, 0, 3); wrap.Controls.Add(preset);
        wrap.Controls.Add(Caption("Custom server / IP and port"));
        var endpoint = new TextBox { Width = 540, Text = prefs.ProxyAddress, PlaceholderText = "socks5://203.0.113.10:1080", MaxLength = 300 }; wrap.Controls.Add(endpoint);
        void Selection() { preset.Enabled = enabled.Checked; endpoint.Enabled = enabled.Checked && preset.SelectedIndex == 3; }
        preset.SelectedIndexChanged += (_, _) => Selection(); enabled.CheckedChanged += (_, _) => Selection(); Selection();
        wrap.Controls.Add(Caption("Presets need an existing service: connect the Mullvad VPN app first, or start your local Tor service. This browser does not include a VPN subscription or a Tor client. Using Tor here does not provide Tor Browser's fingerprinting protections.", 10));
        wrap.Controls.Add(Caption("To change your public IP, choose another working proxy server or change the exit location in your VPN app. Typing an arbitrary IP does not assign it to you. Custom proxies must not require a username/password. An unavailable proxy stops browser requests; there is no direct fallback in this proxy configuration.", 10));
        var savedStatus = Caption(""); savedStatus.Name = "ConnectionStatus";
        wrap.Controls.Add(savedStatus);
        wrap.Controls.Add(ActionButton("Save connection", () => {
            try { var updated = prefs with { ProxyEnabled = enabled.Checked, ProxyPreset = preset.SelectedIndex, ProxyAddress = endpoint.Text.Trim() }; ConnectionPolicy.Endpoint(updated); updated.Save(Path.Combine(root, "preferences.json")); prefs.ProxyEnabled = updated.ProxyEnabled; prefs.ProxyPreset = updated.ProxyPreset; prefs.ProxyAddress = updated.ProxyAddress; savedStatus.Text = "Saved. Restart N0VA to apply this connection.\nActive now: " + (connection == "" ? "Windows connection" : connection); }
            catch (Exception e) { MessageBox.Show(dialog, e.Message); }
        }));
        wrap.Controls.Add(ActionButton("Check current public IP", () => { dialog.Close(); _ = Navigate(AddTab(), "https://api.ipify.org/"); }));
        wrap.Controls.Add(Caption("The IP check contacts ipify through the current browser route. It reports the public IP seen by that service; it is not a VPN connection or leak test.", 9));
        wrap.Controls.Add(ActionButton("Windows VPN settings", () => Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true })));
        wrap.Controls.Add(ActionButton("Done", dialog.Close));
        dialog.ShowDialog(this);
    }
}
