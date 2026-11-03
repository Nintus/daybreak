# Native day view and history QA

November 3, 2026. The C# / WPF 0.4.0 client was tested on Windows with `--test-calendar`. Its version was set before compiling the build used for these UI checks. No Windows security setting was changed.

## Automated verification

All 31 core/storage checks pass. Added coverage verifies complete persisted create/edit/delete undo/redo cycles, reversible backup restores, failed commit/undo/redo behavior, isolation from mutable event lists, no-op saves, redo invalidation, history retention, invalid edits, and date navigation across month/year/leap-day boundaries. The Release Windows build succeeds with zero warnings and zero errors.

## Observed through computer use

- Opened the isolated test calendar and switched Month to Day. The selected date remained November 3.
- Verified two overlapping timed events displayed side by side in the single-day timeline.
- Selected Next; the view advanced to November 4 and showed its all-day event above the timeline.
- Opened that existing event, changed its title, and saved with Enter.
- Ctrl+Z restored the original title, with an “Undid edit event” save status. Ctrl+Y reapplied the title and displayed the corresponding redo status.
- Focused Search, entered a query, and used Ctrl+Z. Search text was undone while the edited event remained unchanged.
- Opened Edit and verified Undo named the available calendar change and Redo was disabled after reapplying it. Chose the menu's Undo while search retained focus; the calendar title reverted.
- Read the synthetic test calendar file and verified that the original event title was persisted to disk after UI Undo.
- Previous returned to November 3 with its two overlapping appointments. The app was left open on that Day view. A screenshot is delivered separately.

Deletion and backup-restore history were exercised by core tests, not destructive UI actions. Only previously created synthetic QA events were edited; personal calendar data was not used.

## Limits

History retains the most recent 20 changes in memory for the current session. Undo/redo changes are saved, but the history itself is not restored after restarting. Existing JSON format and date/time semantics are unchanged. UI restart behavior, arbitrary DPI/minimum-width resizing, screen readers, performance and battery use were not re-tested in this change. Signing remains pending; this successful launch does not guarantee that Windows will trust subsequent unsigned builds.
