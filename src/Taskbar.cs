using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FocusDesktop
{
    /// <summary>
    /// Hides the taskbar windows and gives their space to the desktop work area, so maximized
    /// windows use the whole screen. Show() puts both back.
    /// </summary>
    internal static class Taskbar
    {
        private const uint SPI_SETWORKAREA = 0x002F;
        private const uint SPIF_SENDCHANGE = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
            public RECT(Rectangle r) { Left = r.Left; Top = r.Top; Right = r.Right; Bottom = r.Bottom; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint winIni);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Work areas as they were before hiding, restored by Show().
        private static readonly List<Rectangle> savedWorkAreas = new List<Rectangle>();

        public static void Hide()
        {
            if (savedWorkAreas.Count == 0)
                foreach (Screen screen in Screen.AllScreens)
                    savedWorkAreas.Add(screen.WorkingArea);

            foreach (IntPtr h in AllTaskbars())
                Native.ShowWindow(h, Native.SW_HIDE);

            foreach (Screen screen in Screen.AllScreens)
                SetWorkArea(screen.Bounds);
        }

        public static void Show()
        {
            foreach (IntPtr h in AllTaskbars())
                if (!IsWindowVisible(h))
                    Native.ShowWindow(h, Native.SW_SHOWNA);

            foreach (Rectangle work in savedWorkAreas)
                SetWorkArea(work);
            savedWorkAreas.Clear();
        }

        private static void SetWorkArea(Rectangle area)
        {
            var rect = new RECT(area);
            SystemParametersInfo(SPI_SETWORKAREA, 0, ref rect, SPIF_SENDCHANGE);
        }

        private static IEnumerable<IntPtr> AllTaskbars()
        {
            IntPtr main = Native.FindWindow("Shell_TrayWnd", null);
            if (main != IntPtr.Zero)
                yield return main;
            IntPtr h = IntPtr.Zero;
            while ((h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
                yield return h;
        }
    }
}
