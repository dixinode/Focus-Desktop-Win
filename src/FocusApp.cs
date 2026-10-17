using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FocusDesktop
{
    /// <summary>Wires the floating button, the tray icon and the settings together.</summary>
    internal sealed class FocusApp : ApplicationContext
    {
        private const int DefaultMarginRight = 16;  // logical px from the right edge
        private const int DefaultMarginTop = 72;    // below the caption buttons of maximized windows

        private readonly Settings settings = Settings.Load();
        private readonly FloatingButton button = new FloatingButton();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem miIcons, miTaskbar, miButton, miAutostart;
        private readonly Timer timer = new Timer { Interval = 500 };
        private Icon trayIconVisible, trayIconHidden;
        private bool? shownState;
        private int ticks;
        private bool exiting;
        private bool taskbarHidden;

        public FocusApp()
        {
            // The previous run ended (crash, kill) while icons were hidden: give them back.
            if (settings.IconsHiddenByApp)
            {
                DesktopIcons.SetVisible(true);
                settings.IconsHiddenByApp = false;
                settings.Save();
            }
            // A previous run may have died with the taskbar hidden.
            if (settings.TaskbarHiddenByApp)
            {
                try { Taskbar.Show(); } catch (Exception ex) { Log.Error("Failed to show the taskbar", ex); }
                settings.TaskbarHiddenByApp = false;
                settings.Save();
            }

            miIcons = new ToolStripMenuItem("Hide icons", null, (s, e) => ToggleIcons());
            miTaskbar = new ToolStripMenuItem("Hide taskbar too", null, (s, e) => ToggleHideTaskbar());
            miButton = new ToolStripMenuItem("Hide button", null, (s, e) => ToggleButton());
            miAutostart = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleAutostart());
            menu.Items.AddRange(new ToolStripItem[]
            {
                miIcons,
                miTaskbar,
                new ToolStripSeparator(),
                miButton,
                miAutostart,
                new ToolStripSeparator(),
                new ToolStripMenuItem("Exit", null, (s, e) => Exit()),
            });
            menu.Opening += (s, e) => UpdateMenu();

            CreateTrayIcons();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleIcons(); };

            button.Clicked += (s, e) => ToggleIcons();
            button.DragFinished += (s, e) => SavePosition();
            button.DpiChangedOnScreen += (s, e) => PlaceButton();
            IntPtr unused = button.Handle; // create the window so it can be placed before it is shown
            PlaceButton();
            if (settings.ButtonVisible) button.Show();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.SessionEnding += OnSessionEnding;

            RefreshState();
            tray.Visible = true;

            timer.Tick += (s, e) => OnTick();
            timer.Start();
        }

        private void ToggleIcons()
        {
            bool? visible = DesktopIcons.AreVisible();
            if (!visible.HasValue)
                return;
            bool target = !visible.Value;
            DesktopIcons.SetVisible(target);
            bool? now = DesktopIcons.AreVisible();
            settings.IconsHiddenByApp = now == false;
            settings.Save();
            RefreshState();
        }

        private void ToggleHideTaskbar()
        {
            settings.HideTaskbar = !settings.HideTaskbar;
            settings.Save();
            SyncTaskbar(DesktopIcons.AreVisible() ?? true);
        }

        /// <summary>
        /// While icons are hidden and the check box is on, the taskbar is hidden too. The
        /// "auto-hide" setting is not used: on Windows 11 26H2 the taskbar ignores it when it
        /// is changed by a program, so the taskbar windows are hidden directly instead.
        /// </summary>
        private void SyncTaskbar(bool iconsVisible)
        {
            try
            {
                if (settings.HideTaskbar && !iconsVisible)
                    HideTaskbar();
                else
                    RestoreTaskbar();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to change the taskbar", ex);
            }
        }

        private void HideTaskbar()
        {
            if (taskbarHidden)
                return;
            Taskbar.Hide();
            taskbarHidden = true;
            settings.TaskbarHiddenByApp = true;
            settings.Save();
            // The tray is gone with the taskbar: keep the button on screen as the way back.
            if (!button.Visible)
            {
                PlaceButton();
                button.Show();
            }
        }

        private void RestoreTaskbar()
        {
            if (taskbarHidden)
            {
                taskbarHidden = false;
                try { Taskbar.Show(); }
                catch (Exception ex) { Log.Error("Failed to show the taskbar", ex); }
                if (!settings.ButtonVisible && !exiting)
                    button.Hide();
            }
            if (settings.TaskbarHiddenByApp)
            {
                settings.TaskbarHiddenByApp = false;
                settings.Save();
            }
        }

        private void ToggleButton()
        {
            settings.ButtonVisible = !settings.ButtonVisible;
            settings.Save();
            if (settings.ButtonVisible)
            {
                PlaceButton();
                button.Show();
                button.BringToTop();
            }
            else
            {
                button.Hide();
            }
        }

        private void ToggleAutostart()
        {
            try
            {
                Settings.Autostart = !Settings.Autostart;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to change autostart", ex);
            }
        }

        private void UpdateMenu()
        {
            bool visible = DesktopIcons.AreVisible() ?? true;
            miIcons.Text = visible ? "Hide icons" : "Show icons";
            miTaskbar.Checked = settings.HideTaskbar;
            miButton.Text = settings.ButtonVisible ? "Hide button" : "Show button";
            try { miAutostart.Checked = Settings.Autostart; }
            catch (Exception ex) { Log.Error("Failed to read autostart", ex); }
        }

        private void OnTick()
        {
            RefreshState();
            if (++ticks % 4 == 0 && settings.ButtonVisible)
                button.BringToTop();
        }

        /// <summary>Shows the real icon state on the button and in the tray (catches Win+D, the desktop menu, Explorer restarts).</summary>
        private void RefreshState()
        {
            bool visible = DesktopIcons.AreVisible() ?? true;
            if (shownState == visible)
                return;
            shownState = visible;
            SyncTaskbar(visible);
            button.IconsVisible = visible;
            tray.Icon = visible ? trayIconVisible : trayIconHidden;
            tray.Text = visible ? "Focus Desktop: icons visible" : "Focus Desktop: icons hidden";
        }

        private int ButtonSizePx() => Scale(settings.ButtonSize);

        private static int Scale(int logical) => (int)Math.Round(logical * Native.PrimaryMonitorDpi() / 96.0);

        private void PlaceButton()
        {
            int size = ButtonSizePx();
            Rectangle area = Screen.PrimaryScreen.Bounds;
            Point p;
            if (settings.PositionX < 0 || settings.PositionY < 0)
            {
                p = new Point(area.Right - size - Scale(DefaultMarginRight), area.Top + Scale(DefaultMarginTop));
            }
            else
            {
                double fx = Math.Max(0, Math.Min(1, settings.PositionX));
                double fy = Math.Max(0, Math.Min(1, settings.PositionY));
                p = new Point(area.Left + (int)Math.Round(fx * (area.Width - size)),
                              area.Top + (int)Math.Round(fy * (area.Height - size)));
            }
            button.Place(FloatingButton.ClampToPrimary(p, size), size);
        }

        private void SavePosition()
        {
            int size = ButtonSizePx();
            Rectangle area = Screen.PrimaryScreen.Bounds;
            settings.PositionX = area.Width > size ? (button.Left - area.Left) / (double)(area.Width - size) : 0;
            settings.PositionY = area.Height > size ? (button.Top - area.Top) / (double)(area.Height - size) : 0;
            settings.Save();
        }

        private void CreateTrayIcons()
        {
            int size = SystemInformation.SmallIconSize.Width;
            int dpi = Native.PrimaryMonitorDpi();
            size = Math.Max(size, (int)Math.Round(16 * dpi / 96.0));
            trayIconVisible = Glyph.RenderIcon(size, true);
            trayIconHidden = Glyph.RenderIcon(size, false);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // Resolution, scaling or monitor layout changed (e.g. second monitor unplugged).
            if (exiting) return;
            PlaceButton();
            Icon oldVisible = trayIconVisible, oldHidden = trayIconHidden;
            CreateTrayIcons();
            shownState = null;
            RefreshState();
            oldVisible?.Dispose();
            oldHidden?.Dispose();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            RestoreIcons();
        }

        private void RestoreIcons()
        {
            try
            {
                DesktopIcons.SetVisible(true);
                settings.IconsHiddenByApp = false;
                settings.Save();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to restore icons", ex);
            }
            RestoreTaskbar();
        }

        private void Exit()
        {
            if (exiting) return;
            exiting = true;
            timer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            RestoreIcons();
            tray.Visible = false;
            tray.Dispose();
            button.Close();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                menu.Dispose();
                button.Dispose();
                trayIconVisible?.Dispose();
                trayIconHidden?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
