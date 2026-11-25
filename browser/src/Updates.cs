namespace Nova;
internal sealed partial class BrowserWindow
{
    bool checkingUpdate;
    async Task CheckForUpdate(bool manual)
    {
        if (checkingUpdate || closing) return;
        checkingUpdate = true;
        try {
            var update = await UpdatePolicy.Fetch(connection);
            if (closing || IsDisposed) return;
            if (update == null) { if (manual) MessageBox.Show(this, "You have the newest published N0VA installer (" + UpdatePolicy.Current + ").", "N0VA updates"); return; }
            if (PromptUpdate(update)) await Navigate(AddTab(), update.Url, downloadHandoff: true);
        } catch (Exception ex) {
            if (!closing && manual) MessageBox.Show(this, "Could not check for updates. Try again later.\n\n" + ex.Message, "N0VA updates");
        } finally { checkingUpdate = false; }
    }
    internal bool PromptUpdate(AppUpdate update)
    {
        using var dialog = new Form { Text = "N0VA · Update available", Size = new Size(550, 300), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Surface, ForeColor = Ink, Font = Font, ShowInTaskbar = false };
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        body.Controls.Add(Caption("N0VA " + update.Version + " is available", 20));
        body.Controls.Add(new Label { Text = "Download the new installer? Run it to finish updating.\nChoosing No keeps this version for now.", Width = 470, Height = 52 });
        var actions = new FlowLayoutPanel { Width = 470, Height = 58 };
        var yes = ActionButton("Yes, download", () => dialog.DialogResult = DialogResult.Yes);
        var no = ActionButton("No", () => dialog.DialogResult = DialogResult.No);
        actions.Controls.Add(yes); actions.Controls.Add(no); body.Controls.Add(actions); dialog.Controls.Add(body); dialog.CancelButton = no;
        return dialog.ShowDialog(this) == DialogResult.Yes;
    }
}
