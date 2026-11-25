namespace Nova;

// Paint only background layers, keeping wallpaper and rounded translucent
// surfaces aligned without invoking native control painting in our buffer.
internal static class SurfaceBackground
{
    public static void PaintParent(Control child, Graphics graphics)
    {
        var state = graphics.Save();
        try {
            graphics.TranslateTransform(-child.Left, -child.Top);
            if (child.Parent is { } parent) PaintLayer(parent, graphics);
            else { using var brush = new SolidBrush(SystemColors.Control); graphics.FillRectangle(brush, child.Bounds); }
        } finally { graphics.Restore(state); }
    }
    static void PaintLayer(Control control, Graphics graphics)
    {
        if (control is HomeSurface home) { home.PaintWallpaper(graphics); return; }
        if (control is SoftSurface surface) { PaintParent(surface, graphics); surface.PaintFill(graphics); return; }
        if (control.BackColor.A < 255) PaintParent(control, graphics);
        using var brush = new SolidBrush(control.BackColor);
        graphics.FillRectangle(brush, control.ClientRectangle);
    }
}
