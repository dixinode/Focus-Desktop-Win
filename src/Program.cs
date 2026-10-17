using System;
using System.Threading;
using System.Windows.Forms;

namespace FocusDesktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, @"Local\FocusDesktop.SingleInstance", out bool firstInstance))
            {
                if (!firstInstance)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => FailSafe(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => FailSafe(e.ExceptionObject as Exception);

                Application.Run(new FocusApp());
                GC.KeepAlive(mutex);
            }
        }

        /// <summary>On any unexpected error: never leave the desktop without icons or the taskbar hidden.</summary>
        private static void FailSafe(Exception ex)
        {
            Log.Error("Unhandled exception", ex);
            try { DesktopIcons.SetVisible(true); } catch { }
            try
            {
                Settings s = Settings.Load();
                if (s.TaskbarHiddenByApp)
                {
                    Taskbar.Show();
                    s.TaskbarHiddenByApp = false;
                    s.Save();
                }
            }
            catch { }
            Environment.Exit(1);
        }
    }
}
