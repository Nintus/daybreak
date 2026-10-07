# Daymark

An open source, local-first calendar app for Windows. A calm interface, a useful
offline core, and a path toward Google, Outlook, and iCloud interoperability.

**Status: 0.2 alpha. This is a working local calendar, not feature parity with
Fantastical, Morgen, or Calendar 366. Provider sync is not implemented.**

## Run on Windows

Extract `release/Daymark-Windows.zip`, keep all files together, and run
`Daymark-Windows/Daymark.exe`. The bundle includes Electron; no development
tools are needed. This unsigned x64 build has no installer or auto-updater.

Closing the window keeps the app in the tray. Use the tray menu to quit.
Notifications require the application to be running at the reminder time.

## Develop

Use Node.js 22 or newer and pnpm 11 or newer:

```sh
pnpm install
pnpm start
pnpm test
```

`pnpm dev` serves a browser preview at `http://127.0.0.1:5173`. Browser preview
uses localStorage; desktop uses a JSON file. They have separate datasets.
Native reminders are available only in desktop mode.

`pnpm dist` builds a portable Windows executable with electron-builder in a
normal development environment. `pnpm bundle:windows` creates an unpack-and-run
ZIP from the installed Electron distribution without native build subprocesses.

The included ZIP was built successfully, but a native launch in the managed
workspace was blocked by its Windows sandbox ACL. Browser functionality and
33 engine/desktop-logic tests passed. Native tray and notifications still need
verification after extraction outside the managed workspace.

## Working features

- Day, week, month, and 30-day agenda views; mini calendar and date navigation.
- Create, edit, delete events; all-day and multi-day events; locations and notes.
- Daily, weekly, monthly, yearly repeats; custom RFC 5545 rules (including BYDAY
  and BYSETPOS); optional end date; added/excluded dates from ICS.
- Named event timezones, DST-aware conversion, embedded VTIMEZONE support.
- Edit one occurrence, this and following events, or the entire series;
  moved exceptions and skipped instances survive ICS round-trips.
- Multiple local calendars, colors, filtering, and sample data in its own calendar.
- Drag non-recurring events to another date or time; overlapping-event layout.
- Reviewed quick entry: `Lunch tomorrow at 12:30pm for 1 hour @ Café`.
- Search titles, locations, notes, and recurrence occurrences across one year
  back and two years ahead. Hidden calendars are excluded from search.
- Local tasks with completion state, due dates, and overdue indicators.
- Dark/light themes, Monday/Sunday week starts, 12/24-hour time, shortcuts.
- `.ics` import preview with destination calendar choice and duplicate detection.
- Preserve attendees, alarms, custom fields, timezone definitions, and overrides
  in ICS export. No invitation messages are sent.
- JSON backups and validated restore; atomic desktop saves with a previous-state backup.
- Windows tray, single-instance handling, native event reminders.

## Limits you should know

Calendar views display the device timezone. Each timed event can have its own
IANA timezone; named-zone recurrences preserve that zone's wall-clock time
through DST. Floating events follow the device's local clock. All-day dates are
date-only. Legacy 0.1 events are converted using the timezone stored in their
preferences; 0.2 reads old JSON backups. Use 0.2 for backups containing custom rules.

ICS supports UTC/floating/named times, date-only events, DAILY through YEARLY
recurrence, RDATE date-times, EXDATE, RECURRENCE-ID, and DURATION. Unknown zones
need a matching embedded VTIMEZONE. RANGE overrides, RDATE periods, multiple
rules per event, and hourly/minutely/secondly recurrence are rejected atomically.
Conflicting timezone definitions require separate exports. VTODO tasks and
calendar-level custom properties are not imported. JSON backups include local tasks.

The library converts DURATION to an explicit end date on export; recurrence
uses the first timed occurrence's elapsed duration. Recurrences that fall in a
nonexistent DST wall time are omitted; COUNT handling for those gaps still needs
further standards testing. At most 100,000 recurrence steps are allowed; exceeding
the bound raises an error rather than returning a silently truncated calendar.

This-and-following edits require a single RRULE and no RDATE values. Existing
future overrides retain their explicit details; their recurrence identities and
skips are rebased when the future series shifts. Whole-series edits retain
existing overrides; their identities are not rebased when the whole series moves.

Imported attendee and alarm metadata are preserved, but there is no invitation
workflow; local reminders use Daymark's reminder field rather than interpreting
every imported VALARM. Imports are local copies, not live sync.

Task editing, undo, reminders on tasks, and drag resizing are not implemented.
Quick entry supports a small documented English grammar, not arbitrary language.

Data is stored unencrypted at `%APPDATA%/Daymark/calendar.json`. The previous
state is `calendar.json.bak`. Keep a separate exported backup for durable
recovery. There is no telemetry, hosted service, or account requirement.

## Architecture

- `src/calendar.mjs`: public calendar helpers, validation, quick entry, sample data.
- `src/ical-engine.mjs`: timezone conversion, recurrence, ICS interchange, scoped edits.
- `src/vendor/ical.js`: unmodified ICAL.js 2.2.1 (MPL 2.0); `pnpm vendor` refreshes
  it from the pinned dependency along with the license.
- `src/import-plan.mjs`: destination-specific import deduplication.
- `src/app.mjs`: UI, editing, persistence orchestration, reminder polling.
- `src/index.html` / `style.css`: accessible semantic controls and responsive layout.
- `desktop/main.cjs`: sandboxed window, tray, atomic saves, file exports, notifications.
- `desktop/preload.cjs`: narrow IPC bridge; no generic filesystem or network exposure.

The renderer has no Node access and a restrictive content security policy.
Navigation and popups are blocked. IPC validates the originating frame.

See [the roadmap](docs/ROADMAP.md), [contributing](CONTRIBUTING.md), and
[validation notes](docs/VALIDATION.md).

Daymark is MIT licensed; bundled dependencies retain their own licenses. See
[third-party notices](THIRD-PARTY-NOTICES.md). The project is local source and
has not been published to GitHub.
