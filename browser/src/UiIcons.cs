using System.Drawing.Drawing2D;
namespace Nova;
internal static class UiIcons
{
    public static Bitmap Create(string kind, Color color)
    {
        var bitmap = new Bitmap(24, 24); using var g = Graphics.FromImage(bitmap); Draw(g, kind, new Rectangle(2, 2, 20, 20), color, false); return bitmap;
    }
    public static void Draw(Graphics g, string kind, Rectangle bounds, Color color, bool filled)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var x = bounds.X; var y = bounds.Y; var w = bounds.Width; var h = bounds.Height;
        if (kind == "Search") { g.DrawEllipse(pen, x + w * .12f, y + h * .12f, w * .53f, h * .53f); g.DrawLine(pen, x + w * .60f, y + h * .60f, x + w * .88f, y + h * .88f); }
        else if (kind == "Star") {
            var points = Enumerable.Range(0, 10).Select(i => { var angle = -Math.PI / 2 + i * Math.PI / 5; var r = i % 2 == 0 ? .45 : .21; return new PointF((float)(x + w * (.5 + r * Math.Cos(angle))), (float)(y + h * (.5 + r * Math.Sin(angle)))); }).ToArray();
            if (filled) { using var brush = new SolidBrush(color); g.FillPolygon(brush, points); } else g.DrawPolygon(pen, points);
        } else {
            g.DrawEllipse(pen, x + 2, y + 2, w - 4, h - 4); g.DrawEllipse(pen, x + w * .32f, y + 2, w * .36f, h - 4);
            g.DrawLine(pen, x + 2, y + h / 2f, x + w - 2, y + h / 2f);
        }
    }
}
