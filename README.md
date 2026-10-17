# Focus Desktop

A tiny Windows 10/11 utility that hides and shows the desktop icons (and optionally the taskbar) with one click.

## Usage

Run `FocusDesktop.exe`. Nothing to install: it targets .NET Framework 4.8, which ships with Windows 11. No administrator rights needed.

- **Floating button** in the top-right corner of the primary monitor. Click it to toggle the icons; press and drag to move it. Dark means the icons are visible, blue with a slash means they are hidden.
- **Tray icon.** Left click toggles the icons. Right click opens the menu: *Hide/Show icons*, *Hide taskbar too*, *Hide/Show button*, *Start with Windows*, *Exit*.
- **Hide taskbar too.** When checked, the taskbar is hidden together with the icons and maximized windows take the whole screen. Showing the icons brings the taskbar back. While the taskbar is hidden the tray is out of reach, so the floating button always stays on screen in this mode.
- **Exit** brings back the icons and the taskbar and removes the button and the tray icon.

Settings and the error log live in `%APPDATA%\FocusDesktop\`. Autostart is stored in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## How it works

- Icons are toggled with the same Explorer command as the desktop menu's *View → Show desktop icons* (`WM_COMMAND 0x7402` sent to `SHELLDLL_DefView`). If that has no effect, it falls back to `ShowWindow(SysListView32)`.
- No state is cached: every 0.5 s the real visibility of the icon window is checked, so the button and the tray never get out of sync after Win+D or the desktop context menu.
- The button is a per-pixel-alpha layered window: no frame, not in Alt+Tab or the taskbar, never steals focus. Every 2 s it re-asserts topmost to stay above borderless full-screen apps.
- The taskbar is hidden by hiding the `Shell_TrayWnd` / `Shell_SecondaryTrayWnd` windows and expanding the work area to the full screen. The built-in *Automatically hide the taskbar* setting is not used: on Windows 11 26H2 the taskbar ignores it when a program changes it.
- Per-Monitor V2 DPI awareness. The button position is stored as a fraction of the screen, so it stays in place after resolution or scaling changes.
- If the app crashes while the icons or the taskbar are hidden, they are restored on the next start.

## Building

Sources are in `src/`. Requires the .NET SDK 6 or newer (works on Windows and Linux):

```
cd src
dotnet build -c Release
```

The exe is written to `src/bin/Release/net48/FocusDesktop.exe`.

The original specification is in [docs/PLAN.md](docs/PLAN.md).
