using System;
using static FocusDesktop.Native;

namespace FocusDesktop
{
    /// <summary>
    /// Reads and changes the real visibility of the desktop icons. Nothing is cached:
    /// every call looks at the actual window, so Win+D, the desktop context menu or an
    /// explorer.exe restart never leave the app out of sync.
    /// </summary>
    internal static class DesktopIcons
    {
        // The same command Explorer runs for desktop menu → View → Show desktop icons.
        private const int CmdToggleDesktopIcons = 0x7402;

        /// <summary>True/false for visible/hidden, null when the desktop window is not found (Explorer not running).</summary>
        public static bool? AreVisible()
        {
            IntPtr defView = FindDefView();
            if (defView == IntPtr.Zero)
                return null;
            IntPtr listView = FindListView(defView);
            return listView != IntPtr.Zero && IsWindowVisible(listView);
        }

        public static void Toggle()
        {
            bool? visible = AreVisible();
            if (visible.HasValue)
                SetVisible(!visible.Value);
        }

        public static void SetVisible(bool visible)
        {
            IntPtr defView = FindDefView();
            if (defView == IntPtr.Zero)
                return;

            IntPtr listView = FindListView(defView);
            if (listView != IntPtr.Zero && IsWindowVisible(listView) == visible)
                return;

            // Preferred path: Explorer's own command. It persists the setting and keeps the
            // check mark in the desktop menu in sync, so Explorer never "undoes" our change.
            SendMessageTimeout(defView, WM_COMMAND, (IntPtr)CmdToggleDesktopIcons, IntPtr.Zero,
                SMTO_ABORTIFHUNG, 1000, out _);

            // Fallback for builds where the command does nothing: hide/show the list view directly.
            listView = FindListView(defView);
            if (listView != IntPtr.Zero && IsWindowVisible(listView) != visible)
                ShowWindow(listView, visible ? SW_SHOWNA : SW_HIDE);
        }

        private static IntPtr FindDefView()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
                return defView;

            // After a wallpaper change or Win+D Windows may move the view into a WorkerW window.
            // Newer Windows 11 builds keep WorkerW under Progman, older ones at the top level.
            foreach (IntPtr parent in new[] { progman, IntPtr.Zero })
            {
                IntPtr worker = IntPtr.Zero;
                while ((worker = FindWindowEx(parent, worker, "WorkerW", null)) != IntPtr.Zero)
                {
                    defView = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero)
                        return defView;
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindListView(IntPtr defView)
        {
            return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
        }
    }
}
