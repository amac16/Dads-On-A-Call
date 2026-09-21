# Dad's On A Call

A small native Windows tray app that displays customizable On Call, Off Call, and Headphones On messages at a chosen position on your primary screen. No network connection, account, or calendar access is needed.

## Run

Double-click **Start Dad's On A Call.cmd**. It builds the app on the first run using the .NET Framework compiler included with Windows. You can also run **dist\DadsOnACall.exe** directly once built, or create a Windows shortcut to it.

- The app starts with all indicators **hidden**.
- **Left-click** the **D** tray icon to toggle **On Call**. If Off Call is showing, this replaces it with On Call.
- **Right-click** for **Start/Stop On Call Indicator**, **Start/Stop Off Call Indicator**, **Start/Stop Headphones On Indicator**, **Settings**, or **Exit**.
- On Call defaults to **Dad's On A Call** with a red background, Off Call defaults to **Dad's Off His Call** with a green background, and Headphones On defaults to **Dad's Listening to Music** with a blue background (`#0080C0`). All messages are editable in Settings. Only one sign is shown at a time; starting one replaces the other. Stopping a sign hides it without starting the other.
- The tray icon is red for On Call, green for Off Call, blue for Headphones On, and slate gray when signs are hidden.
- If the icon is hidden, open the taskbar's hidden-icons menu. You can drag it into the visible tray area.
- Closing Settings leaves the tray app running. Exit from the tray menu to quit.

## Settings

Under **Signs of Dad.**, the shared width and height controls (screen pixels) apply to all signs. The **On Call**, **Off Call**, and **Headphones On** tabs each have an editable message, independent background color, font family, font size (points), font color, timer controls, and an appearance preview. Messages support up to 200 characters; leaving one blank restores that sign's default message. Edits update the preview as you type. **Save settings** persists all tabs and updates an active indicator immediately; **Cancel** discards edits in all tabs. On small displays, scroll the settings window to reach all controls.

### Screen Position

Click a section of the small **Screen position** monitor to choose one of nine presets: top left, top center, top right, center left, center, center right, bottom left, bottom center, or bottom right. The selected section is highlighted and its name appears below the monitor.

Position is shared by all messages and defaults to **top right**. Clicking moves a visible sign **immediately**, without Save, and preserves its countdown and any quick-added time. If all signs are hidden, choosing a position does not turn them on; the next sign you start uses that position. Switching modes while Settings is open also uses the live selection.

**Save settings** remembers the position for future runs. **Cancel**, Escape, or closing Settings with X restores the previously saved position. Placement uses the primary display's working area so the taskbar is avoided.

Settings are stored per Windows user at `%LOCALAPPDATA%\DadsOnCall\settings.xml`. Existing appearance settings are retained for On Call when upgrading; Off Call receives its green defaults. The active sign and running countdown are deliberately not saved, so launching the app never announces a call automatically.

Check **Start with Windows** and click **Save settings** to launch the tray app automatically when you sign in to Windows. Both signs still start **hidden**. Uncheck it and save to disable automatic startup; Cancel leaves it unchanged. No administrator permission is required.

Startup is registered as `DadsOnCall` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, separately from appearance settings. Keep the executable in its current location; if you move it, launch it from the new location and save with **Start with Windows** checked to update the path. If Windows Settings or Task Manager has disabled the startup app, re-enable it there as well.

The borderless indicator is always on top of ordinary desktop windows, does not appear in the taskbar or Alt+Tab, and does not steal keyboard focus. It is anchored to the chosen position on the primary display, avoiding the taskbar. Its size is capped to fit the display, and its position follows primary-monitor/work-area changes. It can cover controls underneath it, including Settings; toggle it off from the tray when necessary.

Very large fonts in a small window can wrap or truncate the message; use the preview to choose a readable combination. Windows secure screens, some exclusive full-screen apps, and other topmost windows can appear above the indicator. It is a local display sign, not an overlay injected into screen-sharing software; whether others see it in a meeting depends on what you share.

## Auto-Hide Timers

Each tab has its own **Automatically hide this indicator after** checkbox, unchecked by default. Check it to enable that sign's duration dropdowns, then save.

- **Hours:** 0 through 24, in steps of 1.
- **Minutes:** 0, 1, 2, 3, 4, 5, 10, 15, 30, or 45. The extra 0-minute option permits whole-hour durations.
- Hours and minutes are added together. Enabled timers must have a duration greater than zero. The initial duration is 0 hours, 5 minutes, but it does nothing until enabled.
- Starting a sign starts a fresh countdown. Expiration hides the sign and resets the tray menu, leaving the app running.
- Stopping a sign or switching modes cancels its old countdown. The replacement sign uses its own full duration; expiry never starts another sign automatically.
- Saving a changed duration or enabling the active sign's timer starts a fresh countdown from that save. Disabling it cancels the countdown. Appearance changes and changes to the other sign's timer do not restart it.
- Elapsed time includes sleep; an expired sign hides when Windows resumes and the app processes its next timer tick.

### Live Countdown And Add Time

Timed signs show a smaller, bold countdown below the main message. It updates every second with the wording `For 4:59 more minutes.` The clock always displays total minutes:seconds; for example, one hour and five minutes appears as `For 65:00 more minutes.` When it reaches zero, the sign hides; it does not switch to the other sign.

A centered **Add time** row at the bottom has **5m**, **10m**, **15m**, **30m**, and **1hr** buttons. Each click adds that duration to the time remaining and updates the countdown immediately. Buttons use a slightly darker version of the sign's background and do not take keyboard focus away from your current app.

Quick-added time applies only to the current countdown, not the saved timer settings. Restarting or switching signs uses that sign's saved duration. Appearance edits preserve added time, while changing the active timer's duration restarts it with the new setting. Untimed signs show neither the countdown nor the buttons.

Both settings previews show this layout when their timer is checked. The timed layout scales down to fit smaller windows; increase the shared dimensions if the text or buttons are too small to read comfortably.

## Build And Test

Requires Windows 10/11 with .NET Framework 4.8. No SDK or NuGet packages are required.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

The tests exercise settings persistence/recovery (including older settings), custom messages and safe text truncation, all nine positions, immediate placement with Save/Cancel behavior, native window styles, both tray modes, independent timer deadlines and cancellation, countdown formatting, quick-add increments, startup registration, and both settings tabs. Timer tests use an injected clock to test long durations without waiting and also exercise the actual Windows Forms timer tick. Startup tests use a temporary registry key and do not change your actual startup entries. UI tests briefly show windows and a tray icon and click an Add time button in each mode, restoring the cursor afterward; run them in an interactive desktop session and avoid moving the mouse during the tests.

Manual smoke test: launch the app, toggle both tray actions, type in another app while toggling, change each tab's appearance and save, set each timer to one minute and confirm auto-hide, restart to confirm settings persist, and exit from the tray. For multiple displays, change the primary display while a sign is visible and check that it follows.

Calendar integration is not included. The call indicator itself remains manually controlled, even when the tray app starts with Windows.
