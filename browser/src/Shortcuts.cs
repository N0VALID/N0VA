using Microsoft.Web.WebView2.Core;
namespace Nova;
internal sealed partial class BrowserWindow
{
    bool fullscreen;
    Rectangle previousBounds;
    FormWindowState previousState;
    readonly SoftButton exitFullscreen = new() { Text = "Exit fullscreen · F11", Width = 160, Height = 34, Visible = false };
    internal int WebAcceleratorCount;
    internal bool IsFullscreen => fullscreen;
    void ToggleFullscreen()
    {
        if (!fullscreen) {
            previousState = WindowState; previousBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            fullscreen = true; WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None; Bounds = Screen.FromControl(this).Bounds;
        } else {
            fullscreen = false; FormBorderStyle = FormBorderStyle.Sizable; Bounds = previousBounds; WindowState = previousState;
        }
        chrome.Visible = !fullscreen; tabs.ShowStrip = !fullscreen; statusBar.Visible = !fullscreen && Current?.Ready == true;
        exitFullscreen.Visible = fullscreen; exitFullscreen.Location = new Point(ClientSize.Width - 175, 8); exitFullscreen.BringToFront();
    }
    static bool IsBrowserShortcut(Keys key) => key is Keys.F11 or (Keys.Control | Keys.L) or (Keys.Control | Keys.T) or (Keys.Control | Keys.W) or (Keys.Control | Keys.J) or (Keys.Control | Keys.D) or (Keys.Control | Keys.R) or (Keys.Control | Keys.Tab) or (Keys.Alt | Keys.Left) or (Keys.Alt | Keys.Right);
    internal bool HandleShortcut(Keys key)
    {
        if (key == Keys.F11) { ToggleFullscreen(); return true; }
        if (key == Keys.Escape && fullscreen) { ToggleFullscreen(); return true; }
        if (key == (Keys.Alt | Keys.Left) && Current?.View?.CanGoBack == true) { History(-1); return true; }
        if (key == (Keys.Alt | Keys.Right) && Current?.View?.CanGoForward == true) { History(1); return true; }
        if (key == (Keys.Control | Keys.L)) { if (fullscreen) ToggleFullscreen(); address.Focus(); address.SelectAll(); return true; }
        if (key == (Keys.Control | Keys.T)) { AddTab(); return true; }
        if (key == (Keys.Control | Keys.W) && Current is { } t) { if (t == homePage) { if (tabs.TabCount > 0) tabs.SelectedIndex = tabs.TabCount - 1; } else CloseTab(t); return true; }
        if (key == (Keys.Control | Keys.J)) { ShowTransfers(); return true; }
        if (key == (Keys.Control | Keys.D)) { BookmarkCurrent(); return true; }
        if (key == (Keys.Control | Keys.R)) { ReloadCurrent(); return true; }
        if (key == (Keys.Control | Keys.Tab) && tabs.TabCount > 0) { tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.TabCount; return true; }
        if (key == Keys.Escape && Current?.Ready == true) { Current.View!.CoreWebView2.Stop(); return true; }
        return false;
    }
    void WebShortcut(KeyEventArgs e)
    {
        var key = e.KeyData;
        if (!IsBrowserShortcut(key) && !(key == Keys.Escape && fullscreen)) return;
        e.Handled = true; e.SuppressKeyPress = true; WebAcceleratorCount++;
        BeginInvoke((Action)(() => { if (!closing) HandleShortcut(key); }));
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys key) => HandleShortcut(key) || base.ProcessCmdKey(ref msg, key);
}
