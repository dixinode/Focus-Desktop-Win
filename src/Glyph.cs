using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FocusDesktop
{
    /// <summary>Draws the round button and the tray icon: a 2x2 grid of "icons", crossed out when they are hidden.</summary>
    internal static class Glyph
    {
        private static readonly Color VisibleBack = Color.FromArgb(225, 32, 33, 36);
        private static readonly Color HiddenBack = Color.FromArgb(240, 37, 117, 252);

        public static Bitmap Render(int size, bool iconsVisible, bool hover, bool pressed, bool forTray = false)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float s = size;
                Color back = iconsVisible ? VisibleBack : HiddenBack;
                if (pressed) back = Shade(back, -30);
                else if (hover) back = Shade(back, 25);

                var circle = new RectangleF(0.5f, 0.5f, s - 1f, s - 1f);
                using (var brush = new SolidBrush(back))
                    g.FillEllipse(brush, circle);
                if (!forTray)
                {
                    // Thin light rim keeps the button visible on dark wallpapers.
                    using (var rim = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, s / 40f)))
                        g.DrawEllipse(rim, circle);
                }

                // 2x2 grid of rounded squares.
                float grid = s * (forTray ? 0.56f : 0.46f);
                float gap = grid * 0.16f;
                float cell = (grid - gap) / 2f;
                float x0 = (s - grid) / 2f, y0 = (s - grid) / 2f;
                int squareAlpha = iconsVisible ? 255 : 110;
                using (var sq = new SolidBrush(Color.FromArgb(squareAlpha, 255, 255, 255)))
                {
                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 2; c++)
                            using (GraphicsPath p = RoundRect(x0 + c * (cell + gap), y0 + r * (cell + gap), cell, cell, cell * 0.28f))
                                g.FillPath(sq, p);
                }

                if (!iconsVisible)
                {
                    float w = Math.Max(1.5f, s * 0.085f);
                    float a = s * 0.27f, b = s - a;
                    // Dark outline under the slash so it reads over the squares.
                    using (var under = new Pen(back, w * 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(under, a, a, b, b);
                    using (var slash = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(slash, a, a, b, b);
                }
            }
            return bmp;
        }

        public static Icon RenderIcon(int size, bool iconsVisible)
        {
            using (Bitmap bmp = Render(size, iconsVisible, false, false, forTray: true))
            {
                IntPtr hIcon = bmp.GetHicon();
                // Clone so the managed Icon owns its own handle and the GDI handle can be freed now.
                using (Icon tmp = Icon.FromHandle(hIcon))
                {
                    Icon copy = (Icon)tmp.Clone();
                    Native.DestroyIcon(hIcon);
                    return copy;
                }
            }
        }

        private static Color Shade(Color c, int delta)
        {
            int Clamp(int v) => Math.Max(0, Math.Min(255, v));
            return Color.FromArgb(c.A, Clamp(c.R + delta), Clamp(c.G + delta), Clamp(c.B + delta));
        }

        private static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
