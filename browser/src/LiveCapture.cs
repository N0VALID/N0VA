namespace Nova;
internal static class LiveCapture
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmFlush();
    public static void Save(Form form, string name)
    {
        var work = Screen.FromControl(form).WorkingArea;
        form.Bounds = new Rectangle(work.Left, work.Top, Math.Min(form.Width, work.Width), Math.Min(form.Height, work.Height));
        form.Activate(); form.Refresh(); Application.DoEvents();
        DwmFlush();
        var bounds = Rectangle.Intersect(form.Bounds, SystemInformation.VirtualScreen);
        using var image = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(image)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        image.Save(Path.Combine(AppContext.BaseDirectory, name));
    }
    static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    public static void VerifyButtons(Form form)
    {
        int checkedCount = 0;
        foreach (var button in Descendants(form).OfType<SoftButton>().Where(b => b.Visible)) {
            var bounds = button.RectangleToScreen(button.ClientRectangle);
            if (!Screen.FromControl(form).WorkingArea.Contains(bounds)) continue;
            using var actual = new Bitmap(button.Width, button.Height);
            using (var g = Graphics.FromImage(actual)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            using var expected = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(expected, button.ClientRectangle);
            int different = 0;
            for (int y = 0; y < button.Height; y++) for (int x = 0; x < button.Width; x++) {
                var a = actual.GetPixel(x, y); var b = expected.GetPixel(x, y);
                if (Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B))) > 50) different++;
            }
            if (different > button.Width * button.Height * .04) {
                actual.Save(Path.Combine(AppContext.BaseDirectory, "painting-actual.png"));
                expected.Save(Path.Combine(AppContext.BaseDirectory, "painting-expected.png"));
                throw new Exception($"Live painting differs from offscreen rendering for '{button.Text}' ({button.AccessibleName}): {different} pixels");
            }
            checkedCount++;
        }
        if (checkedCount == 0) throw new Exception("No visible buttons checked for live paint corruption");
    }
}
