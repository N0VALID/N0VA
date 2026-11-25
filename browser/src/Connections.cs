using System.Diagnostics;
namespace Nova;
internal sealed partial class BrowserWindow
{
    internal Func<string, Task<RouteCheck>> ProbeRoute = endpoint => ConnectionProbe.Check(endpoint);
    internal bool RestartRequested;
    internal void OpenConnection()
    {
        using var dialog = new Form { Text = "N0VA · Connection & IP", Size = new Size(660, 735), StartPosition = FormStartPosition.CenterParent, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var wrap = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28), FlowDirection = FlowDirection.TopDown, WrapContents = false }; dialog.Controls.Add(wrap);
        wrap.Controls.Add(Caption("Connection & IP", 24));
        wrap.Controls.Add(Caption(connection == "" ? "Active now: Windows connection / your VPN app" : "Active now: " + connection));
        var enabled = new CheckBox { Text = "Route N0VA through a proxy", Checked = prefs.ProxyEnabled, AutoSize = true }; wrap.Controls.Add(enabled);
        var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 540 }; preset.Items.AddRange(ConnectionPolicy.Presets); preset.SelectedIndex = Math.Clamp(prefs.ProxyPreset, 0, 3); wrap.Controls.Add(preset);
        var instruction = Caption(""); wrap.Controls.Add(instruction);
        var endpoint = new TextBox { Width = 540, Text = prefs.ProxyAddress, PlaceholderText = "socks5://server:port", MaxLength = 300, AccessibleName = "Custom proxy address" }; wrap.Controls.Add(endpoint);
        var result = Caption(""); result.Name = "ConnectionStatus"; wrap.Controls.Add(result);
        var actions = new FlowLayoutPanel { Width = 550, Height = 54 };
        var test = ActionButton("Test connection", () => { }); var save = ActionButton("Save connection", () => { }); actions.Controls.Add(test); actions.Controls.Add(save); wrap.Controls.Add(actions);
        var restart = ActionButton("Restart now", () => { dialog.Close(); RestartRequested = true; Close(); if (!closing) RestartRequested = false; }); restart.Visible = false; wrap.Controls.Add(restart);
        Preferences Draft() => prefs with { ProxyEnabled = enabled.Checked, ProxyPreset = preset.SelectedIndex, ProxyAddress = endpoint.Text.Trim() };
        void Selection() {
            preset.Enabled = enabled.Checked; endpoint.Visible = enabled.Checked && preset.SelectedIndex == 3; result.Text = "";
            instruction.Text = !enabled.Checked ? "Uses your normal Windows connection, including an already connected VPN app." : preset.SelectedIndex switch {
                0 => "Connect the Mullvad VPN app first. This preset uses its private SOCKS5 server; selecting it does not connect or buy Mullvad.",
                1 => "Start a local Tor service on port 9050 first. If you use Tor Browser, choose its port 9150 preset instead.",
                2 => "Open Tor Browser and wait for it to connect. Keep it open while N0VA uses its local port 9150.",
                _ => "Enter a working HTTP, HTTPS or SOCKS5 proxy with a port. Username/password proxies are not supported."
            };
        }
        preset.SelectedIndexChanged += (_, _) => Selection(); enabled.CheckedChanged += (_, _) => Selection(); Selection();
        async Task<bool> Check(Preferences draft) {
            var route = ConnectionPolicy.Endpoint(draft); if (route.Length == 0) { result.Text = "Windows connection selected."; return true; }
            result.Text = "Testing this proxy…"; var checkedRoute = await ProbeRoute(route);
            if (dialog.IsDisposed) return false;
            result.Text = checkedRoute.Message + (checkedRoute.Success ? "" : "\n" + instruction.Text + "\nYour saved connection was not changed.");
            return checkedRoute.Success;
        }
        Button? useWindows = null;
        async Task Run(bool persist) {
            test.Enabled = save.Enabled = enabled.Enabled = preset.Enabled = endpoint.Enabled = restart.Enabled = false; if (useWindows != null) useWindows.Enabled = false;
            try {
                var draft = Draft(); if (!await Check(draft) || dialog.IsDisposed || !persist) return;
                draft.Save(Path.Combine(root, "preferences.json")); prefs.ProxyEnabled = draft.ProxyEnabled; prefs.ProxyPreset = draft.ProxyPreset; prefs.ProxyAddress = draft.ProxyAddress;
                result.Text += "\nSaved. Restart N0VA to apply this connection."; restart.Visible = true;
            } catch (Exception ex) { if (!dialog.IsDisposed) result.Text = ex.Message; }
            finally { if (!dialog.IsDisposed) { test.Enabled = save.Enabled = enabled.Enabled = restart.Enabled = true; if (useWindows != null) useWindows.Enabled = true; preset.Enabled = enabled.Checked; endpoint.Enabled = enabled.Checked && preset.SelectedIndex == 3; } }
        }
        test.Click += async (_, _) => await Run(false); save.Click += async (_, _) => await Run(true);
        useWindows = ActionButton("Use Windows connection", () => {
            enabled.Checked = false; prefs.ProxyEnabled = false; prefs.Save(Path.Combine(root, "preferences.json"));
            result.Text = "Saved. Restart N0VA to use Windows / your VPN app.\nThis switches off the browser proxy only."; restart.Visible = true;
        }); wrap.Controls.Add(useWindows);
        wrap.Controls.Add(Caption("Testing contacts ipify through the selected proxy. A successful test shows that route worked at that moment. An unavailable proxy never silently falls back to a direct connection. No VPN subscription or Tor client is bundled.", 9));
        wrap.Controls.Add(Caption("For Tor Browser's privacy protections, use Tor Browser itself. N0VA only supports its proxy route.", 9));
        wrap.Controls.Add(ActionButton("Windows VPN settings", () => Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true })));
        wrap.Controls.Add(ActionButton("Done", dialog.Close)); dialog.ShowDialog(this);
    }
    void ShowConnectionError(SiteTab tab)
    {
        if (tab.IsDisposed || closing) return;
        tab.Home?.Dispose(); tab.View!.Visible = false;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(44), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Surface, ForeColor = Ink };
        panel.Controls.Add(Caption("This proxy could not load the page", 25));
        panel.Controls.Add(Caption("Active proxy: " + connection + "\nCheck that its VPN or Tor service is running, or choose Windows connection and restart."));
        panel.Controls.Add(ActionButton("Connection settings", OpenConnection));
        panel.Controls.Add(ActionButton("Try again", () => _ = Navigate(tab, tab.Pending)));
        tab.Home = panel; tab.Controls.Add(panel); panel.BringToFront();
    }
}
