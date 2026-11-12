using System;
using System.Drawing;
using System.Drawing.Drawing2D;


namespace FunWrite
{
    public static class GlyphRenderer
    {
        public static void Draw(Graphics g, Stroke stroke, RectangleF bounds, Func<int, Color> color,
            double progress = 1, bool numbers = false)
        {
            if (stroke == null || stroke.StrokeList.Count == 0) return;
            float scale = .8f * Math.Min(bounds.Width / Math.Max(1, stroke.Width), bounds.Height / Math.Max(1, stroke.Height));
            float left = bounds.Left + (bounds.Width - stroke.Width * scale) / 2;
            float top = bounds.Top + (bounds.Height - stroke.Height * scale) / 2;
            int count = 0; foreach (var line in stroke.StrokeList) count += line.Count;
            int remaining = (int)Math.Ceiling(count * Math.Max(0, Math.Min(1, progress)));
            using (var font = new Font("Microsoft JhengHei", 10, FontStyle.Bold))
            for (int i = 0; i < stroke.StrokeList.Count && remaining > 0; i++)
            {
                var line = stroke.StrokeList[i];
                int length = Math.Min(line.Count, remaining); remaining -= line.Count;
                using (var pen = new Pen(color(i), Math.Max(2, bounds.Width / 30)))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    Func<Sample, PointF> point = p => new PointF(left + (p.X - stroke.Left) * scale,
                        top + (stroke.Bottom - p.Y) * scale);
                    for (int j = 1; j < length; j++) g.DrawLine(pen, point(line[j - 1]), point(line[j]));
                    if (length == 1)
                    { var p = point(line[0]); g.DrawEllipse(pen, p.X, p.Y, 1, 1); }
                    if (numbers && length > 0)
                    { var p = point(line[0]); using (var brush = new SolidBrush(Color.DarkBlue)) g.DrawString((i + 1).ToString(), font, brush, p.X - 15, p.Y - 15); }
                }
            }
        }

        public static void Paper(Graphics g, RectangleF rect, bool writing = false)
        {
            using (var fill = new SolidBrush(writing ? Color.LightYellow : Color.White)) g.FillRectangle(fill, rect);
            using (var pen = new Pen(Color.LightCoral, 1))
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                pen.DashStyle = DashStyle.Dash;
                g.DrawLine(pen, rect.Left + rect.Width / 2, rect.Top, rect.Left + rect.Width / 2, rect.Bottom);
                g.DrawLine(pen, rect.Left, rect.Top + rect.Height / 2, rect.Right, rect.Top + rect.Height / 2);
            }
        }
    }
}
