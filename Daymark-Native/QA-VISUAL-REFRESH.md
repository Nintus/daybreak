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
