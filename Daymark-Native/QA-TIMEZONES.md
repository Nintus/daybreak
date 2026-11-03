# Timezone foundation QA

November 3, 2026 — native C# / WPF 0.5.0 on Windows, using `--test-calendar`. The existing preview PRs #1–#5 were reviewed against their successful checks and merged into main before preparing this change.

## Automated checks

All 47 core/storage checks pass. Timezone coverage includes winter/summer conversions, skipped and repeated hours, an interval ending earlier on the clock but later in UTC, half-hour DST changes, quarter-hour offsets, cross-date projection, display-zone Today, all-day/floating invariance, zoned backup round trips, legacy load without rewrite, an unrotated migration backup, malformed/unknown timezone metadata and undo/redo preserving instants. Release WPF compilation passes with zero warnings and errors.

## Observed through Windows computer use

- Opened the legacy QA calendar. Its file remained version 1 with three events and no migration backup before saving; the migration notice explained floating events. Existing clock times were unchanged.
- Created “Timezone QA · Travel call” on November 3, 18:00–19:00 in America/New_York. It displayed at 17:00–18:00 in the device's America/Chicago zone.
- Verified the first save wrote version 2, retained America/New_York as the source zone, and stored 23:00Z–00:00Z. The separate version-1 backup retained the original three events.
- Chose Asia/Tokyo for display. The anchored event moved to November 4, 08:00–09:00; floating events stayed at their original clock times and the all-day event stayed on November 4. Today highlighting followed Tokyo's date.
- Opened the anchored event from Tokyo's projected agenda. The editor correctly showed November 3, 18:00–19:00 in New York.
- Changed that draft to March 8, 02:30–03:30. Save was rejected with a clear nonexistent-time error; the editor stayed open.
- Changed the draft to November 1, 01:30–01:45. Separate start/end occurrence pickers appeared. Save without a choice was rejected.
- Selected the first start occurrence and second end occurrence, then saved. The file contained 05:30Z–06:45Z, a 75-minute elapsed interval. Tokyo displayed 14:30–15:45.
- Used Ctrl+Z to restore the prior travel event. A direct UTC read from JSON confirmed its original 23:00Z start and America/New_York source zone. An initial diagnostic reparsed a timestamp through local time; the corrected check read UTC directly.
- Left the app in Day view on November 4 in Tokyo, showing the restored travel event and unchanged all-day event. The screenshot is delivered separately. Agenda descriptions wrap instead of requiring horizontal scrolling.

Only synthetic local QA data was used. No Windows timezone or security settings were changed.

## Limits and compatibility

The automatic device-zone option refreshes when the window is activated; changing the actual Windows timezone was not exercised. Display-zone selection is per session. Timeline rows represent wall-clock hours, so repeated-hour occurrences share grid space; descriptions show offsets. Recurrence, invitations, provider sync, reminders and timezone-database rule-update scenarios are not implemented or validated by these checks.

Legacy events remain floating until explicitly anchored. Version-2 data is not readable by older previews. Loading alone does not rewrite the file; first save retains `.v1.bak` separately from the rotating `.bak`. New anchored events use stored UTC instants as their authority. Signing remains pending despite this successful local launch.

Final automated guards also cover preserving subminute precision when displayed minute fields are unchanged, and rejecting oversized saves before an unreadable file can be produced. These two edge cases were verified by core tests; the UI scenarios above used minute-aligned events.
