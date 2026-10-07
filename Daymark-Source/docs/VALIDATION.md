# Validation — 2026-10-07

## Version 0.2 additions

All **33 automated tests pass**. New coverage includes IANA and embedded custom
zones across DST, ambiguous/nonexistent times, UTC UNTIL bounds, BYDAY/BYSETPOS,
RDATE/EXDATE, scoped occurrence changes, moved exceptions entering a range,
UTC recurrence IDs on zoned series, differing start/end zones, metadata and alarm
preservation, count-limited series splitting, exception identity rebasing, and
destination-specific import deduplication.

New browser checks verified:

- Creating a Europe/London event with a six-instance Wednesday/Friday rule;
  the view displayed all six in the device's America/Chicago timezone.
- Moving a single occurrence to a different day retained six total instances
  and survived reload.
- Editing this and following events split the series into two masters while
  retaining six total instances and the earlier moved exception.
- Importing a synthetic America/New_York ICS through the file chooser showed a
  preview, accepted the Work destination, and displayed three occurrences.
- The recurring event editor exposes scope, timezone, and a custom rule field.

The source remains compatible with 0.1 data. Native limitations described below
remain: the native tray and actual reminder delivery still need external testing.

## Automated

`pnpm test` exercises the calendar engine and main-process logic without
launching UI or connecting external accounts. Tests cover date ranges,
recurrence limits/exceptions, skipped month days, leap years, DST wall-clock
behavior, all-day exclusive ends, quick-entry parsing, ICS escaping and folding,
unsupported recurrence/timezone rules, invalid dates, and import rejection.

Desktop tests use an Electron API mock with real filesystem writes to a unique
temporary directory. They verify renderer isolation configuration, navigation
blocking, untrusted IPC rejection, ordered saves, previous-state backup,
canceled exports, unsupported notifications, and corrupt-file handling.

## Browser UI checks

Verified the following through the running localhost preview:

- Empty first run; explicitly add a sample calendar with seven events.
- Quick entry parses a title, relative date, time, duration, and location into
  a review dialog; saving creates an event and search finds it.
- Data persists through page reload. Editing updates the search result.
- Weekly recurrence expands in search; skipping one occurrence reduces results
  by one without removing the series.
- Day view renders one day column; month view renders 42 date cells.
- Local tasks can be created with a due date using the narrow-window dialog.
- Calendar visibility filtering hides a calendar's events.
- Layout inspected at the ordinary browser width and at 1440 × 940 desktop size.
- No browser console errors observed during the checked flows.

`preview.jpg` shows sample data, not imported personal calendar data.

## Windows bundle

The Electron runtime downloaded successfully and the unpack-and-run ZIP was
created with the full runtime and app files. The normal electron-builder flow
encountered a sandbox subprocess restriction, so the ZIP was built using the
included PowerShell bundle script.

Attempted a hidden native launch using an isolated test data directory. Electron
stopped before creating the renderer because the workspace's Windows ACL does
not grant its sandbox token read access to the runtime folder. No system or
directory permissions were changed to work around this.

**Native startup, tray interactions, actual notifications, and save-dialog UI
are not verified in this environment.** Extract the ZIP outside this managed
workspace and run the smoke checks below before relying on it as a daily driver.

## Native smoke checks still required

1. Extract all files; launch Daymark.exe; create an event and a task.
2. Quit via tray, reopen, and confirm persistence.
3. Create an event one minute ahead with an at-start reminder; close the window;
   verify the notification while the app is in the tray.
4. Export ICS and JSON, restore the JSON, and verify dates and tasks.
5. Import a supported ICS fixture; verify rejected advanced files leave data intact.
6. Test suspend/resume, Windows notification settings, minimum window size,
   screen reader navigation, system timezone changes, and corrupt-file recovery.

Google, Outlook, and iCloud connections have not been implemented or tested.
