# Daymark native Windows preview

A real Windows desktop client built with C# and WPF on .NET 10. The UI uses Windows desktop controls; there is no Electron, Chromium, WebView, local web server, or Node.js process.

This is the first native client, not feature parity with the JavaScript calendar. It supports a month grid, a selected-day agenda, creating/editing/deleting timed and all-day events, title/location/notes search, and native JSON backup/restore. It works without network access.

Choose **Week** in the view switcher for a Monday–Sunday timeline. Previous/Next move one week; Today returns to the current date. Select a day heading to use it for New event. Double-click an empty time slot to start an event at that half-hour; click an event to edit it. All-day events appear above the timeline, overnight appointments appear on each day they overlap, and simultaneous events share the day's width. Short appointments have a minimum display height; their saved times are unchanged. Scroll vertically for the full 24 hours. Narrow windows also allow horizontal scrolling.

Choose **Day** for the same timeline focused on one date. Previous/Next move one day, including across month and year boundaries. Switching Month/Week/Day keeps your selected date; month navigation keeps the day number where possible and clamps it in shorter months.

The **Edit** menu offers calendar **Undo** and **Redo** for event creation, editing, deletion and backup restores. Each action is saved to disk, and a failed save leaves the current document and history intact. The most recent 20 changes are available during the current session; closing the app clears this history. A new change after Undo clears Redo. Use Ctrl+Z for Undo, Ctrl+Y or Ctrl+Shift+Z for Redo when working in the calendar. Text fields keep their own text-editing undo behavior; the Edit menu explicitly acts on calendar changes. Exporting a backup does not add a history step.

## Interface

The native interface uses a compact Month/Week/Day switcher, event previews in the month grid, and a selected-day schedule with readable time and zone summaries. Use **File** for backup/restore and **Help** for About and time-zone guidance. Search filters the schedule across the calendar. The editor groups start and end date/time fields; all-day dates retain their exclusive-end meaning. Keyboard focus is visible. High contrast uses system control templates and colors; switching contrast while the app is open requires restarting it.

## Time zones and daylight saving

New timed events default to the current display zone. The editor stores an IANA timezone ID and UTC start/end instants. Displaying the calendar in another zone moves those events to the corresponding local date and time; editing still opens the original event zone. Choose **Device time zone (automatic)** to follow the computer's zone; it is refreshed when the app is activated. **Today** and date highlighting use the display zone. The display-zone choice is per session.

The editor rejects a clock time skipped during a spring-forward transition. For a repeated hour, choose its first or second occurrence separately for the start and end. Display descriptions include UTC offsets to distinguish repeated times. Changing an event's zone in the editor keeps its entered clock values and changes its instants; changing the calendar's display zone never changes stored events.

All-day events remain date ranges in every zone. **Floating** timed events keep their clock values everywhere. Existing version-1 events migrate as floating events because their original timezone was never recorded. Set a zone explicitly in the editor to anchor one. Migration does not rewrite a file merely by loading it. The first successful save writes version 2 and preserves the original in `calendar.json.v1.bak`; this migration backup is not rotated. The ordinary previous-save `.bak` continues separately. Export a backup before returning to an older native preview: older versions reject version-2 calendars and cannot read newly zoned events.

The 24-hour timeline uses clock-time rows; repeated-hour occurrences share that part of the grid and are distinguished by offsets in descriptions. It is not a 25-row elapsed-hours view. UTC instants are authoritative for anchored non-recurring events; stored clock fields are snapshots. OS timezone data supplies conversion rules. Recurrence and provider sync require further work.

## Run the prepared Windows build

1. Download `Daymark-Native-Windows-x64.zip` from the task's prepared files.
2. Right-click the ZIP in File Explorer and choose **Extract All**.
3. Open the extracted `Daymark-Native-Windows` folder.
4. Double-click **Daymark.exe**. Keep the other files next to it.

Use Windows 10 or Windows 11 on an x64 PC. This self-contained build includes the .NET desktop runtime: you do not need Node.js, pnpm, a browser, or a separate .NET installation. The extracted directory is approximately 141 MB, mostly runtime libraries. Disk size is not a measurement of live memory usage.

The preview is unsigned; code signing and installer distribution are not completed. Do not disable Windows security settings. A successful cross-build does not establish native launch, display, accessibility, or performance results on Windows.

## Code signing policy

Read the [code signing policy](docs/CODE_SIGNING.md), [privacy policy](docs/PRIVACY.md) and [SignPath readiness checklist](docs/SIGNPATH_READINESS.md). Acceptance and integration are pending; current previews remain unsigned.

## Data and limitations

Events save to `%LOCALAPPDATA%\DaymarkNative\calendar.json`. The previous successful state is `calendar.json.bak`. Saves validate the entire document, flush a temporary file, and replace the data file. Invalid imports do not replace existing data. Startup stops if the data file is damaged and displays its location instead of overwriting it with an empty calendar.

Closing the window exits the app. It does not install a startup service, remain in the tray, poll providers, or run reminder timers. A single-instance guard prevents ordinary duplicate launches in the same Windows session.

Recurrence, reminders, tasks, ICS import/export, provider synchronization, and migration from web/Electron data are **not implemented**. Its backup format and data location are separate from those of the existing clients. Do not use a web backup as a native backup or assume existing events transfer automatically.

All-day event end dates are exclusive: to include Friday and Saturday, set the start to Friday and the end to Sunday. Native date pickers, keyboard tab navigation, `Ctrl+N` for a new event, `Ctrl+B` for backup, and Escape to cancel the editor are available.

## Build on Windows

The active direction is **Windows first**, with portable calendar logic and platform-specific interfaces. See [the roadmap](ROADMAP.md) for the implementation order and cloud/local handoff rules.

**Help → About Daymark** shows the version, source commit and data location. Automated checks publish a commit-named Windows artifact without replacing a public release. Build artifacts are retained for 14 days.

For manual QA, launch `Daymark.exe --test-calendar`. Its title explicitly says **Test calendar**, and its data is stored in `TestCalendar/calendar.json` beside the executable. It has a separate single-instance guard, so your personal calendar can stay open. The test folder must be writable; test data persists between launches. Do not distribute that folder with app packages.

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

The shared core has 47 checks covering storage, visible-range boundaries, timeline overlap layout, navigation, undo/redo, timezone conversion, DST gaps/folds, fractional offsets and legacy migration. The month view scans the projected event collection once per refresh instead of once for every visible day. This reduces repeated work; it is not yet a claim about large-calendar performance.

Before relying on the app, check these on Windows with disposable events using the test calendar:

1. Launch; create a timed event and a two-day all-day event; switch months and select their dates.
2. Edit title, location, notes and time; search by each field; delete with cancel and then confirmation.
3. Close and reopen; verify all saved events.
4. Export a backup, add an event, restore the backup, and verify the extra event is removed only after confirmation.
5. Try an invalid backup and an end before start; confirm neither changes saved data.
6. Test Tab, Enter, Escape, Ctrl+N, screen scaling and window resizing.
7. Measure resources using Task Manager: wait a minute after launch, record idle CPU and memory, then compare with the web/Electron version using equivalent events. Repeat with a representative event count. Battery comparisons need sustained tests under the same device settings.

## Efficiency goals

The design avoids a browser runtime and has no idle polling or background network activity. Calendar rendering happens on navigation or changes; event saves happen on explicit edits. These choices provide a useful foundation, but resource use and battery life have not yet been measured. Apple Calendar-level efficiency is a goal, not a result claimed by this preview.
