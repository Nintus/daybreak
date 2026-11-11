# Daymark native Windows preview

A real Windows desktop client built with C# and WPF on .NET 10. The UI uses Windows desktop controls; there is no Electron, Chromium, WebView, local web server, or Node.js process.

This native preview supports Month, Week, Day and Agenda views, calendar sets, tasks and time blocks, timed and all-day events, recurrence and exceptions, Quick Add, reminders, `.ics` transfer, and native backup and undo. These daily planning features work offline.

Choose **Week** in the view switcher for a Monday–Sunday timeline. Previous/Next move one week; Today returns to the current date. Select a day heading to use it for New event. Double-click an empty time slot to start an event at that half-hour; click an event to edit it. All-day events appear above the timeline, overnight appointments appear on each day they overlap, and simultaneous events share the day's width. Short appointments have a minimum display height; their saved times are unchanged. Scroll vertically for the full 24 hours. Narrow windows also allow horizontal scrolling.

Choose **Day** for the same timeline focused on one date. Previous/Next move one day, including across month and year boundaries. Switching Month/Week/Day keeps your selected date; month navigation keeps the day number where possible and clamps it in shorter months.

The **Edit** menu offers calendar **Undo** and **Redo** for event creation, editing, deletion and backup restores. Each action is saved to disk, and a failed save leaves the current document and history intact. The most recent 20 changes are available during the current session; closing the app clears this history. A new change after Undo clears Redo. Use Ctrl+Z for Undo, Ctrl+Y or Ctrl+Shift+Z for Redo when working in the calendar. Text fields keep their own text-editing undo behavior; the Edit menu explicitly acts on calendar changes. Exporting a backup does not add a history step.

## Interface

The native interface uses a compact Month/Week/Day switcher, event previews in the month grid, and a selected-day schedule with readable time and zone summaries. Use **File** for backup/restore and **Help** for About and time-zone guidance. Search filters the schedule across the calendar. The editor groups start and end date/time fields; all-day dates retain their exclusive-end meaning. Keyboard focus is visible. The schedule and search controls adapt to compact windows (minimum width 860).

Choose **Appearance → System setting**, **Light**, or **Dark**. Changes apply immediately, including menus, dropdowns, date popups and title bars. System mode follows the Windows app-color preference and refreshes on Windows preference notifications or app activation. Your choice is saved in `appearance.txt` beside the native calendar data; the test calendar has its own preference. Missing, invalid or unreadable preferences fall back to System without blocking calendar access. Calendar backups contain events, not appearance preferences. High contrast takes precedence and uses system control templates and colors; switching contrast while the app is open requires restarting it.

## Calendars, repeating events and search

Use **Manage calendars** to add, rename and recolor local calendars. Save applies the current name and color edit. Choose a calendar in the event editor. Moving an entire series to another calendar moves its exceptions too. Calendar names and colors are included in native backups.

Open **Calendar sets** to save groups such as Work or Personal. Each set chooses visible calendars, an optional default local calendar for new events, and whether Agenda shows due tasks. The selector switches between sets. Changing a checkbox switches to custom visibility. Set preferences live in `calendar-sets.json` alongside the native calendar. The app loads older visibility preferences from `hidden-calendars.json` when it first opens a profile.

The editor supports daily, weekly (including multiple weekdays), monthly date/ordinal-weekday/last-weekday, and yearly date repeats, with an interval, inclusive end date or up to 10,000 occurrences. Opening a repeating event asks whether to edit this occurrence or the entire series. A moved occurrence keeps its original identity; deleting one stores a skipped-occurrence exception. A series schedule change with exceptions requires explicit confirmation to reset those exceptions. Simple title/location/calendar changes preserve them. “This and following” splitting and a dedicated skipped-occurrence restoration screen are not implemented; session Undo can restore a skip.

Monthly dates such as the 31st skip months without that date; February 29 skips non-leap years. Last-weekday repeats require a matching start date. Timed repeats keep the source local start clock and a fixed elapsed duration across DST. Nonexistent starts are skipped without consuming the occurrence count; repeated-hour starts use the seed's first/second choice when available, otherwise the first occurrence. All-day repeats preserve date duration. These are explicit local semantics, not a claim to support every provider/RFC recurrence rule.

Search includes title, location and notes, honors hidden calendars, and returns up to 200 matches. Single appointments are searched across all dates; repeating events are searched from one month before the selected date through the next year. Results show dates, and the search header explains the recurrence window. Ctrl+G jumps directly to a date without stepping month by month.

## Time zones and daylight saving

New timed events default to the current display zone. The editor stores an IANA timezone ID and UTC start/end instants. Displaying the calendar in another zone moves those events to the corresponding local date and time; editing still opens the original event zone. Choose **Device time zone (automatic)** to follow the computer's zone; it is refreshed when the app is activated. **Today** and date highlighting use the display zone. The display-zone choice is per session.

The editor rejects a clock time skipped during a spring-forward transition. For a repeated hour, choose its first or second occurrence separately for the start and end. Display descriptions include UTC offsets to distinguish repeated times. Changing an event's zone in the editor keeps its entered clock values and changes its instants; changing the calendar's display zone never changes stored events.

All-day events remain date ranges in every zone. **Floating** timed events keep their clock values everywhere. Existing version-1 events migrate as floating events because their original timezone was never recorded. Set a zone explicitly in the editor to anchor one. Migration does not rewrite a file merely by loading it. The first successful save writes version 3 and preserves the original version-1 or version-2 file in `calendar.json.v1.bak` or `calendar.json.v2.bak`; this migration backup is not rotated. The ordinary previous-save `.bak` continues separately. Export a backup before returning to an older native preview: older previews reject version-3 calendars. Version 3 adds calendars, repeat rules and exception identities; do not downgrade your writable data file.

The 24-hour timeline uses clock-time rows; repeated-hour occurrences share that part of the grid and are distinguished by offsets in descriptions. It is not a 25-row elapsed-hours view. UTC instants are authoritative for anchored non-recurring events; stored clock fields are snapshots. OS timezone data supplies conversion rules. Recurring series use their source wall-clock schedule and the seed event's elapsed duration; connected provider instances are read-only projections from bounded snapshots.

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

Closing the window exits the app by default. Enable **Reminders → Keep running after closing** to keep local reminders active in the Windows notification area. Use **File → Exit** or **Quit Daymark** in the tray to quit. Launch Daymark again after signing in to Windows. A single-instance guard prevents duplicate launches of the same calendar in a Windows session.

## Quick Add and reminders

Use **Ctrl+Shift+N** to open Quick Add. Try `Lunch tomorrow at noon for 45m` or `Focus next Monday at 9am every week remind 15m before`. Use English weekday names, `today`, `tomorrow`, or a date such as `2026-12-15`. A phrase with a date and no time creates an all-day draft. Choose **Review event** to check the draft, calendar and time zone in the standard editor before saving.

Use the event editor to set a reminder for a local event or series. Timed reminders count back from the event start. All-day reminders count back from 9 AM in the device time zone. Daymark's reminder window offers **Dismiss** and **Snooze 5 min**, with both choices saved for the individual alert. **Reminders → Show reminders** reopens the due list. While Daymark is running, it checks the next deadline and replans after calendar edits, device clock changes and resume. After restart or sleep, reminders can still appear for up to 12 hours after their deadline and before their event ends.

Native backups carry event reminder settings. Calendars with reminders use version 4 and require Daymark v0.12 or later. The first upgrade from version 3 preserves the previous file in `calendar.json.v3.bak`. Snoozes and dismissals live separately in `reminder-state.json`. Set reminders in Daymark after importing an `.ics` file.

## Plan with Agenda and Tasks

Select **Agenda** to see two weeks of events and open task deadlines from the selected date. Open a date header to jump to Day view. Open a row to edit its event or task. Completed tasks are available in **File → Tasks**. Use **Ctrl+Shift+T** to open Tasks directly.

Tasks can have a due date, notes and an importance flag. **Schedule time** opens the event editor to reserve a linked block on a local calendar. Completing or deleting a task keeps its calendar commitment. Deleting the linked event frees the task to be scheduled again. The native backup and Undo include tasks and links. Calendars containing tasks use version 5 and require Daymark v0.13 or later. The first upgrade preserves the previous file with its version in the backup name.

Drag a timed local event in Day or Week to move it in 15-minute increments. For keyboard rescheduling, select an event in the Schedule list and press **Ctrl+M**. Moving a repeating appointment moves the selected occurrence. Anchored events keep their source time zone and elapsed duration. You can undo the move.

Use **File → Import from another app…** to review discovered Fantastical, classic Outlook and Thunderbird account identities, compatible settings, and local .ics files. Imported Google/Microsoft profiles appear in **Calendar accounts** with sign-in hints; iCloud/CalDAV profiles are listed pending provider support. Add exported .ics files manually when a source uses a private database. See [migration support and limits](docs/IMPORT.md).

Use **File → Export calendar (.ics)…** to save one local calendar for an inclusive date range of up to 1,096 days. Daymark uses its display time zone to select overlapping events and includes each event's full duration. Repeating events become individual occurrences, including moves and edits. Skipped occurrences stay out of the file. Use **Save backup** to preserve complete repeating series and all local calendars.

Tasks are stored locally. Two-way provider synchronization and web/Electron data migration are future work. The [near-term backlog](docs/NEAR_TERM_BACKLOG.md) tracks the Windows release, connected calendar work, and planning improvements.

All-day event end dates are exclusive: to include Friday and Saturday, set the start to Friday and the end to Sunday. Native date pickers, keyboard tab navigation, `Ctrl+N` for a new event, `Ctrl+B` for backup, `Ctrl+F` for search, `Ctrl+G` for Go to Date, and Escape to cancel the editor are available.

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

The portable test suite covers storage, time zones, recurrence and exceptions, import and export, sync planning, Quick Add parsing, and reminder deadlines and recovery. Windows checks exercise the native editor, migration preview, tray lifecycle and the reminder window with local fixtures. The month view scans the projected event collection once per refresh.

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

## Connected accounts in 0.9

Use **File → Calendar accounts** for Google/Microsoft setup, browser sign-in, calendar selection and manual read-only refresh. See [ACCOUNT_SETUP.md](docs/ACCOUNT_SETUP.md). Provider-expanded events remain separate from local editing/backup/history. The downloaded range is three months back and fifteen months forward at refresh time; cache coverage and oldest refresh appear in the status bar. No background polling is installed. Real sign-in still requires OAuth registrations and end-to-end validation. Two-way editing is not enabled.

Unchanged window activations now retain the month visual tree; theme refreshes skip unchanged palette resources. Windows CI checks both behaviors and measures unattended idle CPU, rejecting failed launches. See [CPU and sync QA](QA-CPU-AND-SYNC.md) for evidence and limitations.

## Theme studio

Use **Appearance → Theme studio** for System, Light, Dark, OLED black, Ocean, Forest, Rose dusk or Custom. Custom accepts a native Windows color picker, quick swatches or a #RRGGBB value and generates matching surfaces, selection colors, borders and readable accent text. Choose a Light, Dark or OLED base. Changes preview immediately across the app; Save theme persists them, while Cancel or closing restores the previous theme. Invalid color text cannot be saved.

OLED black uses pure #000000 canvas and calendar surfaces, with outlines and colored selection/event fills for legibility. Calendar colors retain their own identities. Windows high contrast takes priority. Existing appearance.txt choices migrate when saved; preferences remain separate from calendar backups. No battery-savings claim is made.

## Motion and navigation

Navigation, settled search results and newly opened windows use a subtle 120 ms opacity transition at up to 30 frames per second. Input and data updates remain immediate; no animation delays saving or opening an event. Rapid navigation replaces the previous transition, and completed/unloaded animations release their clocks. There are no perpetual animation loops or background motion timers.

Windows client-area animation and high-contrast preferences take priority. **Appearance → Reduce motion** also disables transitions and stops active ones immediately; this preference is saved separately in motion.txt. Theme-color previews remain immediate. Selecting another day inside the current month updates the existing grid and agenda, preserving focus rather than rebuilding all 42 day buttons.

Windows CI tests motion policy, replacement/cleanup and selection reuse. Actual motion feel on the target PC still needs a signed build and visual verification; the existing Smart App Control blocker remains.

## Horizon identity

The app header, window/taskbar icon and default accent now use the Horizon sunrise-D identity. The wordmark uses bundled Manrope SemiBold; interface text stays in Segoe UI. Existing saved themes and named calendar colors are retained. [Brand source and palette](docs/BRAND.md) document the editable mark, icon generation and font license.
