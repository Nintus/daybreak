# Daymark native Windows preview

A real Windows desktop client built with C# and WPF on .NET 10. The UI uses Windows desktop controls; there is no Electron, Chromium, WebView, local web server, or Node.js process.

This is the first native client, not feature parity with the JavaScript calendar. It supports a month grid, a selected-day agenda, creating/editing/deleting timed and all-day events, title/location/notes search, and native JSON backup/restore. It works without network access.

## Run the prepared Windows build

1. Download `Daymark-Native-Windows-x64.zip` from the task's prepared files.
2. Right-click the ZIP in File Explorer and choose **Extract All**.
3. Open the extracted `Daymark-Native-Windows` folder.
4. Double-click **Daymark.exe**. Keep the other files next to it.

Use Windows 10 or Windows 11 on an x64 PC. This self-contained build includes the .NET desktop runtime: you do not need Node.js, pnpm, a browser, or a separate .NET installation. The extracted directory is approximately 141 MB, mostly runtime libraries. Disk size is not a measurement of live memory usage.

The preview is unsigned; code signing and installer distribution are not completed. Do not disable Windows security settings. A successful cross-build does not establish native launch, display, accessibility, or performance results on Windows.

## Data and limitations

Events save to `%LOCALAPPDATA%\DaymarkNative\calendar.json`. The previous successful state is `calendar.json.bak`. Saves validate the entire document, flush a temporary file, and replace the data file. Invalid imports do not replace existing data. Startup stops if the data file is damaged and displays its location instead of overwriting it with an empty calendar.

Closing the window exits the app. It does not install a startup service, remain in the tray, poll providers, or run reminder timers. A single-instance guard prevents ordinary duplicate launches in the same Windows session.

This preview uses device-local wall-clock dates and times. Recurrence, timezone-aware events, reminders, tasks, ICS import/export, provider synchronization, and migration from web/Electron data are **not implemented**. Its backup format and data location are separate from those of the existing clients. Do not use a web backup as a native backup or assume existing events transfer automatically.

All-day event end dates are exclusive: to include Friday and Saturday, set the start to Friday and the end to Sunday. Native date pickers, keyboard tab navigation, `Ctrl+N` for a new event, `Ctrl+B` for backup, and Escape to cancel the editor are available.

## Build on Windows

Install the **.NET 10 SDK** from Microsoft's official .NET download page, then open a terminal in `Daymark-Native`:

```powershell
dotnet run --project Tests/Daymark.Tests.csproj -c Release
dotnet run --project Windows/Daymark.Windows.csproj
```

To produce a self-contained folder:

```powershell
dotnet publish Windows/Daymark.Windows.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Copy the complete output folder. ARM64 Windows needs a separate `win-arm64` build, which has not been prepared or tested.

## Validation performed

The shared core's 12 checks passed on Linux: event round-trips; previous-state backup; rejected saves preserving existing data; duplicate IDs; unsupported versions; midnight overlap boundaries; exclusive all-day ends; local-time validation; backup restore; and preserving corrupt data on load. The Windows Release build and self-contained x64 publish completed successfully.

The WPF UI cannot run in this Linux workspace. Before relying on the app, check these on Windows with disposable events:

1. Launch; create a timed event and a two-day all-day event; switch months and select their dates.
2. Edit title, location, notes and time; search by each field; delete with cancel and then confirmation.
3. Close and reopen; verify all saved events.
4. Export a backup, add an event, restore the backup, and verify the extra event is removed only after confirmation.
5. Try an invalid backup and an end before start; confirm neither changes saved data.
6. Test Tab, Enter, Escape, Ctrl+N, screen scaling and window resizing.
7. Measure resources using Task Manager: wait a minute after launch, record idle CPU and memory, then compare with the web/Electron version using equivalent events. Repeat with a representative event count. Battery comparisons need sustained tests under the same device settings.

## Efficiency goals

The design avoids a browser runtime and has no idle polling or background network activity. Calendar rendering happens on navigation or changes; event saves happen on explicit edits. These choices provide a useful foundation, but resource use and battery life have not yet been measured. Apple Calendar-level efficiency is a goal, not a result claimed by this preview.
