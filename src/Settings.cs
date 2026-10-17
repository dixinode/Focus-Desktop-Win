using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace FocusDesktop
{
    /// <summary>Settings in %APPDATA%\FocusDesktop\settings.ini.</summary>
    internal sealed class Settings
    {
        public static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusDesktop");

        private static readonly string FilePath = Path.Combine(Folder, "settings.ini");
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "FocusDesktop";

        /// <summary>
        /// Button position as a fraction (0..1) of the free space on the primary monitor,
        /// so it stays in the same relative place when the resolution or scaling changes.
        /// Negative means "not set yet": use the default top-right position.
        /// </summary>
        public double PositionX = -1;
        public double PositionY = -1;

        /// <summary>Button size in logical pixels (at 100% scaling).</summary>
        public int ButtonSize = 40;

        public bool ButtonVisible = true;

        /// <summary>
        /// True while icons are hidden by this app. If it is still true on start-up the
        /// previous run crashed with icons hidden, so they are restored.
        /// </summary>
        public bool IconsHiddenByApp;

        /// <summary>Menu check box: also auto-hide the taskbar while the icons are hidden.</summary>
        public bool HideTaskbar;

        /// <summary>True while this app has the taskbar hidden and owes a restore.</summary>
        public bool TaskbarHiddenByApp;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return s;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                        values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                s.PositionX = ReadDouble(values, "PositionX", s.PositionX);
                s.PositionY = ReadDouble(values, "PositionY", s.PositionY);
                s.ButtonSize = Math.Max(24, Math.Min(96, (int)ReadDouble(values, "ButtonSize", s.ButtonSize)));
                s.ButtonVisible = ReadBool(values, "ButtonVisible", s.ButtonVisible);
                s.IconsHiddenByApp = ReadBool(values, "IconsHiddenByApp", s.IconsHiddenByApp);
                s.HideTaskbar = ReadBool(values, "HideTaskbar", s.HideTaskbar);
                s.TaskbarHiddenByApp = ReadBool(values, "TaskbarHiddenByApp", s.TaskbarHiddenByApp);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read settings", ex);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var ic = CultureInfo.InvariantCulture;
                File.WriteAllLines(FilePath, new[]
                {
                    "[FocusDesktop]",
                    "PositionX=" + PositionX.ToString("0.#####", ic),
                    "PositionY=" + PositionY.ToString("0.#####", ic),
                    "ButtonSize=" + ButtonSize.ToString(ic),
                    "ButtonVisible=" + (ButtonVisible ? "1" : "0"),
                    "IconsHiddenByApp=" + (IconsHiddenByApp ? "1" : "0"),
                    "HideTaskbar=" + (HideTaskbar ? "1" : "0"),
                    "TaskbarHiddenByApp=" + (TaskbarHiddenByApp ? "1" : "0"),
                });
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save settings", ex);
            }
        }

        /// <summary>Autostart lives in HKCU\...\Run: the registry itself is the source of truth, no admin rights needed.</summary>
        public static bool Autostart
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key?.GetValue(RunValue) is string;
            }
            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                        key.SetValue(RunValue, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue(RunValue, false);
                }
            }
        }

        private static double ReadDouble(Dictionary<string, string> values, string name, double fallback)
        {
            return values.TryGetValue(name, out string v) &&
                   double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                ? d : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string name, bool fallback)
        {
            return values.TryGetValue(name, out string v) ? v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) : fallback;
        }
    }

    internal static class Log
    {
        public static void Error(string message, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(Path.Combine(Settings.Folder, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine + ex + Environment.NewLine);
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }
}
