namespace Nova;
internal static class LiveCapture
{
    public static void Save(Form form, string name)
    {
        form.Activate(); form.Refresh(); Application.DoEvents();
        var bounds = Rectangle.Intersect(form.Bounds, SystemInformation.VirtualScreen);
        using var image = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(image)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        image.Save(Path.Combine(AppContext.BaseDirectory, name));
    }
}
