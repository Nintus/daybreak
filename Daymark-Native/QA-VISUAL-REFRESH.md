# Native visual refresh verification — November 3, 2026

Client: C# / WPF, version 0.6.0, isolated `--test-calendar` profile. This refresh builds on the timezone branch.

## Appearance

- Unified blue/teal palette, softer grid lines, consistent typography, rounded calendar and schedule surfaces.
- Month/Week/Day segmented switcher, compact navigation, search placeholder and primary New event action.
- Adaptive month previews with hidden-event counts; schedule cards show concise local times, display zone/UTC offset and location.
- File menu contains backup/restore; Help contains timezone guidance and About. Calendar undo/redo remains in Edit.
- Compact editor groups start/end date and time fields. All-day guidance appears when relevant; native date and zone controls remain in use.
- Standard high-contrast control templates are retained with system brushes. Contrast-mode changes require an app restart.

## Verified locally

- 47 core checks pass. Release Windows build: zero warnings/errors.
- Real Windows launch and restart of the existing version-2 QA calendar succeeded.
- Visually inspected Month, Week and Day views, overlapping timed events, all-day strip, event counts and summaries.
- Search with no matches displayed the dedicated empty state; clearing it restored the selected-day schedule.
- New-event keyboard shortcut opened the compact editor. Title/location text entry, all-day disabled fields and exclusive-end guidance were checked. Escape cancelled an unsaved draft.
- Edited a synthetic event location and saved via Alt+S; the schedule showed the new location. Edit > Undo restored the original event and persisted it.
- Selected-event styling and enabled edit actions were checked. Maximized layout remained aligned.
- Screenshots of the native month/week views and compact editor were saved with the task deliverables.

## Remaining checks

High-contrast mode, alternate DPI/scaling, screen-reader behavior and minimum-size layout need dedicated Windows QA. A border-drag attempt did not establish a smaller window size. No security or Windows accessibility settings were changed. No performance/battery benchmark was performed. Provider sync, recurrence, reminders and signed distribution remain separate work.


## Theme and compact-layout follow-up — version 0.7.0

Added Appearance > System setting / Light / Dark. Palette brushes update in place to preserve view state and calendar history. The preference is saved separately from events, atomically replaced, and isolated in the test profile. High contrast overrides the selected palette. System preference notifications and app activation refresh System mode. Window and editor title bars use the Windows dark-titlebar attribute where supported.

Local verification:

- Release build succeeds with zero warnings/errors; all 47 core checks pass.
- System mode resolved to the machine's current dark app preference without changing Windows settings.
- Switched to Light live and restarted: Light persisted. Switched to Dark and restarted: Dark persisted.
- Dark palette was inspected in the month grid, schedule cards, menu, dropdown and event editor. Time-zone selection labels render as labels rather than object representations.
- The date editor uses themed native DatePicker parts and calendar day/month buttons. Date selection changed the date field correctly and closed the popup. Draft was cancelled without saving an event.
- Snapped the calendar to the left: the screenshot was 849 pixels wide (860 window width including frame). Toolbar, month navigation, month grid and schedule remained usable; schedule/search narrowed automatically.

The template parts follow the [WPF Calendar documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/calendar) and [DatePicker documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/datepicker). OS theme changes while the app stays active, high contrast, alternate DPI and screen readers still need dedicated verification; no OS settings were changed in this session.

Final template verification confirmed weekday headers, switching to year selection and back to the month grid. Scrollbars now follow the palette; the final week view was checked for appearance and scrolling.
