using System;
using System.Drawing;
using System.Windows.Forms;
using static FocusDesktop.Native;

namespace FocusDesktop
{
    /// <summary>
    /// Round always-on-top button. A per-pixel-alpha layered window: no frame, not in
    /// Alt+Tab or the taskbar, never steals focus. Click toggles, press-and-drag moves it.
    /// </summary>
    internal sealed class FloatingButton : Form
    {
        public event EventHandler Clicked;
        public event EventHandler DragFinished;
        public event EventHandler DpiChangedOnScreen;

        private bool iconsVisible = true;
        private bool hover, pressed, dragging;
        private Point downCursor, downLocation;
        private int buttonSize = 40;

        public FloatingButton()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Focus Desktop";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public bool IconsVisible
        {
            get => iconsVisible;
            set { if (iconsVisible != value) { iconsVisible = value; Render(); } }
        }

        /// <summary>Places the button (physical pixels) and redraws it.</summary>
        public void Place(Point location, int sizePx)
        {
            buttonSize = sizePx;
            Bounds = new Rectangle(location, new Size(sizePx, sizePx));
            Render();
        }

        /// <summary>Re-asserts topmost so the button stays above full-screen (borderless) apps that grab the top of the z-order.</summary>
        public void BringToTop()
        {
            if (Visible)
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static Point ClampToPrimary(Point p, int size)
        {
            Rectangle area = Screen.PrimaryScreen.Bounds;
            return new Point(
                Math.Max(area.Left, Math.Min(p.X, area.Right - size)),
                Math.Max(area.Top, Math.Min(p.Y, area.Bottom - size)));
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Render();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = true;
            dragging = false;
            downCursor = Cursor.Position;
            downLocation = Location;
            Render();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!hover) { hover = true; Render(); }
            if (!pressed) return;

            Point c = Cursor.Position;
            int dx = c.X - downCursor.X, dy = c.Y - downCursor.Y;
            if (!dragging && (Math.Abs(dx) > SystemInformation.DragSize.Width || Math.Abs(dy) > SystemInformation.DragSize.Height))
                dragging = true;
            if (dragging)
            {
                Location = ClampToPrimary(new Point(downLocation.X + dx, downLocation.Y + dy), buttonSize);
                Render();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pressed) return;
            bool wasDrag = dragging;
            pressed = dragging = false;
            Render();
            if (wasDrag) DragFinished?.Invoke(this, EventArgs.Empty);
            else Clicked?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover && !pressed) { hover = false; Render(); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Layered window: everything is drawn by Render().
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
                case WM_DPICHANGED:
                    // Scaling of the primary monitor changed: the owner recomputes size and position.
                    DpiChangedOnScreen?.Invoke(this, EventArgs.Empty);
                    m.Result = IntPtr.Zero;
                    return;
            }
            base.WndProc(ref m);
        }

        private void Render()
        {
            if (!IsHandleCreated || buttonSize <= 0) return;

            using (Bitmap bmp = Glyph.Render(buttonSize, iconsVisible, hover, pressed))
            {
                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memDc = CreateCompatibleDC(screenDc);
                IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr old = SelectObject(memDc, hBitmap);
                try
                {
                    var size = new SIZE(bmp.Width, bmp.Height);
                    var src = new POINT(0, 0);
                    var dst = new POINT(Left, Top);
                    var blend = new BLENDFUNCTION
                    {
                        BlendOp = AC_SRC_OVER,
                        SourceConstantAlpha = 255,
                        AlphaFormat = AC_SRC_ALPHA,
                    };
                    UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    SelectObject(memDc, old);
                    DeleteObject(hBitmap);
                    DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }
    }
}
