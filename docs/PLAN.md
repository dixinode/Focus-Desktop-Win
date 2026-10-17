# Focus Desktop: implementation plan

A small Windows 11 utility. It hides the desktop icons with one button so they don't distract you while you work.

## 1. Goal

- A floating button on top of all windows on the primary monitor. It can be dragged anywhere.
- Clicking the button toggles the visibility of the desktop icons.
- A tray icon. Left click toggles the icons. Right click opens a menu.
- Exiting the program removes the button and the tray icon and brings the icons back to the desktop.

## 2. Decisions

| Question | Decision |
|---|---|
| Where is the button | Primary monitor only |
| On top of what | All windows, including full-screen apps |
| Tray left click | Toggles the icons: show → hide → show... |
| Tray right click | Menu |
| Hotkey | Not needed |
| Auto-hide when an app has focus | Not needed. Manual toggle only |

## 3. Functional requirements

### 3.1 Floating button
- Borderless window without a title bar, on top of all windows.
- Not shown in Alt+Tab or on the taskbar.
- Drag with the mouse: hold the left button and move.
- On first run it appears in the top-right corner of the primary monitor.
- The position is kept between runs.
- If the saved position is off screen (e.g. the resolution changed), the button is moved into the visible area.
- Its look reflects the current state: e.g. a different glyph or color when the icons are hidden.

### 3.2 Toggling the icons
- Clicking the button and left-clicking the tray icon do the same thing.
- The state is not kept in a variable. The real visibility of the icon window is checked every time, so the program never gets out of sync if the icons were toggled from the desktop context menu or with Win+D.

### 3.3 Tray icon
- A regular icon in the notification area.
- Left click: toggle the icons.
- Right click: menu:
  - "Hide icons" / "Show icons" (text depends on the state);
  - "Show/hide button";
  - "Start with Windows" (stage 4, optional);
  - "Exit".

### 3.4 Exit
- Removes the button from the screen.
- Removes the tray icon.
- Brings back the desktop icons if they were hidden.
- The process ends completely and does not linger in Task Manager.

## 4. Technical approach

### 4.1 Stack
- **Prototype:** AutoHotkey v2. The quick way to check that it works.
- **Production version (optional):** Python (ctypes + pystray or PyQt) or C# (WinForms/WPF). Chosen after the prototype.

### 4.2 Hiding the desktop icons
1. Find the `Progman` window, inside it `SHELLDLL_DefView`, then `SysListView32`.
2. If it is not under Progman, check the `WorkerW` windows. Windows creates them on wallpaper changes and on Win+D.
3. Visibility: `IsWindowVisible(listview)`.
4. Hide: `ShowWindow(listview, SW_HIDE)`. Show: `ShowWindow(listview, SW_SHOW)`.
5. For reference: desktop right click → View → Show desktop icons. Same effect via `WM_COMMAND` 0x7402.

### 4.3 Dragging the button
- AutoHotkey: `PostMessage(0xA1, 2, 0)` (simulates grabbing the title bar) or handle `WM_LBUTTONDOWN` and recompute the window position.
- Other stacks: the standard mechanism for dragging a borderless window.

### 4.4 DPI and monitor
- Enable DPI awareness. Otherwise at 125% and 150% scaling the button ends up in the wrong place.
- Store the position relative to the primary monitor, not in absolute pixels.

### 4.5 Settings
- A `settings.ini` or `settings.json` file in `%APPDATA%\FocusDesktop\`.
- Fields: button position, size, autostart.

## 5. Stages

### Stage 1. Prototype (AutoHotkey v2)
- [ ] Borderless button, on top of all windows, not in Alt+Tab.
- [ ] Click toggles the desktop icons.
- [ ] The state is taken from the real window visibility.

### Stage 2. Dragging and position
- [ ] Drag the button with the mouse.
- [ ] Save the position to the settings file.
- [ ] Restore the position on start.
- [ ] Guard against an off-screen position.

### Stage 3. Tray and menu
- [ ] Tray icon.
- [ ] Left click toggles the icons.
- [ ] Right click: menu with all items from 3.3.
- [ ] Menu item text depends on the state.
- [ ] Exit: restore icons, remove the button and the tray icon, terminate completely.

### Stage 4. Optional
- [ ] Start with Windows (via the Startup folder or a shortcut).

### Stage 5. Optional
- [ ] Rewrite in Python or C# if the prototype works out and an installer is needed.

## 6. Testing

- [ ] The button can be dragged and stays in place after a restart.
- [ ] 100%, 125%, 150% scaling: the button is in place and clickable.
- [ ] Two monitors: the button is on the primary one and is not lost after the second monitor is disconnected.
- [ ] Icons hide and come back with the button and with a left click on the tray icon.
- [ ] No desync: if the icons were toggled from the desktop context menu or with Win+D, the button and the menu show the right state.
- [ ] The button is visible over a full-screen app.
- [ ] Exit restores the icons and removes the button and the tray icon.
- [ ] Restarting `explorer.exe` does not break the program.
- [ ] Works without administrator rights.

## 7. Risks

- `Progman` and `WorkerW` behavior may differ between Windows 11 builds. Test on several builds before release.
- Antivirus software sometimes flags AutoHotkey scripts. For the production version it is better to build an exe or use Python or C#.
- If the program crashes while the icons are hidden, they must be restored on the next start.
