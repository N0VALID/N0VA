using System.Collections.ObjectModel;

namespace Nova;

internal class SmoothPanel : Panel
{
    public SmoothPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

// Native buttons and a plain buffered surface avoid the system TabControl's
// unthemed border and repaint artifacts around a resizing WebView2 child.
internal sealed class TabDeck : SmoothPanel
{
    readonly FlowLayoutPanel strip = new() { Dock = DockStyle.Top, Height = 42, WrapContents = false, AutoScroll = true, Padding = new Padding(4, 3, 4, 3) };
    readonly Button plus = new SoftButton { Text = "+", AccessibleName = "Open home to add a website", Width = 34, Height = 33, Margin = new Padding(4, 0, 4, 0) };
    readonly Dictionary<SiteTab, (Panel Row, Button Title, Button Close)> buttons = [];
    int selected = -1;
    Control? home;
    public void SetHome(Control page) { home = page; page.Dock = DockStyle.Fill; Controls.Add(page); page.BringToFront(); strip.SendToBack(); page.Visible = selected < 0; }
    Color ink = Color.WhiteSmoke, accent = Color.DarkSeaGreen;
    public Collection<SiteTab> TabPages { get; }
    public int TabCount => TabPages.Count;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public SiteTab? SelectedTab { get => selected >= 0 && selected < TabCount ? TabPages[selected] : null; set => SelectedIndex = value == null ? -1 : TabPages.IndexOf(value); }
    public event EventHandler? SelectedIndexChanged;
    public event Action<SiteTab>? CloseRequested;
    public event Action? NewRequested;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex {
        get => selected;
        set {
            if (value < -1 || value >= TabCount || value == selected) return;
            SuspendLayout(); selected = value;
            for (int i = 0; i < TabCount; i++) TabPages[i].Visible = i == selected;
            if (home != null) home.Visible = selected < 0;
            ResumeLayout(true); RefreshTitles(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public TabDeck() { TabPages = new Pages(this); Controls.Add(strip); strip.Controls.Add(plus); plus.Click += (_, _) => NewRequested?.Invoke(); strip.Visible = false; }
    sealed class Pages(TabDeck owner) : Collection<SiteTab>
    {
        protected override void InsertItem(int index, SiteTab tab) { base.InsertItem(index, tab); owner.Add(tab); }
        protected override void RemoveItem(int index) { var tab = this[index]; base.RemoveItem(index); owner.Remove(tab, index); }
    }
    void Add(SiteTab tab)
    {
        strip.Visible = true;
        tab.Dock = DockStyle.Fill; tab.Visible = false; Controls.Add(tab); tab.BringToFront(); strip.SendToBack();
        var row = new SmoothPanel { Width = 195, Height = 33, Padding = new Padding(0, 0, 0, 3), Margin = new Padding(2, 0, 2, 0) };
        var title = new Button { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), AutoEllipsis = true, UseMnemonic = false };
        var close = new Button { Dock = DockStyle.Right, Width = 26, FlatStyle = FlatStyle.Flat, Text = "×", AccessibleName = "Close tab" };
        title.FlatAppearance.BorderSize = close.FlatAppearance.BorderSize = 0;
        title.Click += (_, _) => SelectedTab = tab; close.Click += (_, _) => CloseRequested?.Invoke(tab);
        title.MouseUp += (_, e) => { if (e.Button == MouseButtons.Middle) CloseRequested?.Invoke(tab); };
        row.Paint += (_, e) => { if (SelectedTab == tab) { using var pen = new Pen(accent, 2); e.Graphics.DrawLine(pen, 4, row.Height - 1, row.Width - 4, row.Height - 1); } };
        row.Controls.Add(title); row.Controls.Add(close); strip.Controls.Add(row); strip.Controls.SetChildIndex(plus, strip.Controls.Count - 1); buttons.Add(tab, (row, title, close)); RefreshTitles();
    }
    void Remove(SiteTab tab, int index)
    {
        Controls.Remove(tab); if (buttons.Remove(tab, out var b)) { strip.Controls.Remove(b.Row); b.Row.Dispose(); }
        strip.Visible = TabCount > 0;
        var next = TabCount == 0 ? -1 : Math.Clamp(selected > index ? selected - 1 : selected, 0, TabCount - 1);
        selected = -2; SelectedIndex = next;
    }
    public void Theme(Color surface, Color foreground, Color highlight) { BackColor = strip.BackColor = plus.BackColor = surface; plus.ForeColor = foreground; ink = foreground; accent = highlight; RefreshTitles(); }
    public void RefreshTitles()
    {
        foreach (var (tab, b) in buttons) {
            b.Title.Text = tab.Text; b.Title.AccessibleName = tab.Text;
            b.Title.BackColor = b.Close.BackColor = b.Row.BackColor = BackColor;
            b.Title.ForeColor = b.Close.ForeColor = ink; b.Row.Invalidate();
        }
    }
    public new void Invalidate() { RefreshTitles(); base.Invalidate(); }
}
