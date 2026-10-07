# Daymark — cloud development handoff

## Product vision and release standard

Build an open source calendar that can become the user's primary calendar:
"I don't need another calendar app." Fantastical's paid version is the benchmark
for capabilities and polish. Morgen and Calendar 366 remain useful references.
Use an original Daymark identity and interface while pursuing comparable depth.

The user has changed the product plan to **web first**, followed by a native
Windows app, iOS, Apple Watch, Android, Linux, and potentially macOS when suitable
hardware is available. This supersedes the earlier Windows-first plan. Moving
development to Codex Cloud and building a hosted web product are separate steps;
both now belong to the plan. Cloud migration has not happened in this workspace.

Feature count alone does not establish parity. The first meaningful release
target is a month of primary-calendar use with the user's actual accounts,
without lost events or needing another app for routine calendar work. Establish
explicit checks for sync correctness, reminder delivery, recurrence, timezone
behavior, recovery, and everyday usability. Do not equate passing unit tests or
a successful browser demo with meeting that release standard.

Prioritize reliability before expanding the feature surface. Duplicate meetings,
missed reminders, lost changes, and edits applied to the wrong recurrence scope
undermine the product even when the interface looks polished.

## User experience principles

- Quiet, immediate, predictable interactions: open the app, understand the day,
  add or change something quickly, and move on.
- Excellent quick entry with a visible interpretation before saving, keyboard
  navigation, search, templates, and direct manipulation.
- Progressive disclosure: reveal advanced controls when they are relevant.
- Clear sync and connection status. Explain failures and show which changes
  remain safely stored or pending; never silently discard or overwrite them.
- Undo and recoverable ordinary changes, including accidental deletions.
- Accessible controls, readable layouts, and responsive design from the web phase.
- Validate complete journeys: receive an invitation, accept it, change its
  reminder, join the meeting, and find it afterward. Test across accounts and
  devices as well as isolated controls.

## Feature parity priorities

1. **Calendar accounts:** dependable Google Calendar, Outlook / Microsoft 365,
   iCloud, and generic CalDAV sync; shared calendars; account permissions;
   explicit connection state. Google, Outlook, and iCloud are user priorities.
2. **Event management:** complex recurrence and exceptions, timezones,
   invitations and RSVP, attachments, conferencing, and correct organizer /
   attendee semantics across providers.
3. **Everyday speed:** strong natural-language entry, keyboard shortcuts,
   search, reusable templates, drag-and-drop, and undo.
4. **Planning:** tasks alongside events, task integrations, time blocking,
   calendar sets, working hours, and multiple timezone displays.
5. **Scheduling:** availability sharing, booking pages, meeting proposals /
   polls, buffers, and conflict prevention.
6. **Platform integration and extras:** dependable reminders, widgets,
   tray/menu access, quick-entry shortcuts, watch complications, weather,
   travel time and departure alerts, and subscribed calendars.

These are a long-term parity target, not a claim about implemented features.
Maintain a feature matrix that records implementation, provider/platform limits,
validation evidence, and remaining work. Each release should be useful by itself.

## Platform sequence and architecture direction

1. **Web:** establish the primary calendar experience, responsive UI, accounts,
   durable storage, secure provider credentials, background sync, and an
   offline-capable client. The current localStorage preview is not a production
   hosted product. Define authentication, authorization, data isolation,
   migrations, backups, and deployment before handling real user calendar data.
2. **Native Windows:** build on the verified web/core foundation and add
   dependable native notifications, tray access, global quick entry, and desktop
   integration. The existing Electron shell is a prototype; do not assume it
   settles the final meaning or implementation of native Windows UX.
3. **iOS:** touch-first interaction, quick capture, widgets, and mobile alerts.
4. **Apple Watch:** focus on the next event, complications, reminders, and brief
   interactions rather than reproducing the entire desktop interface.
5. **Android:** share calendar semantics while respecting Android interaction
   patterns and platform integrations.
6. **Linux**, then **potentially macOS:** reuse desktop infrastructure where
   useful and provide platform-specific polish. macOS is contingent on access
   to suitable hardware; iOS/watchOS development also needs supported Mac/Xcode
   access, so account for that earlier in the sequence.

Share calendar logic, data contracts, provider abstractions, and design
principles. Choose client frameworks deliberately; do not assume every platform
should use an identical UI or that the current direct-DOM implementation is the
final frontend architecture. Keep calendar semantics independent of any shell.

The open-source direction should support a complete self-hostable product.
Optional managed hosting may eventually help fund operations, but pricing and
funding are undecided. Account for ongoing hosting, push delivery, and weather /
maps licensing costs rather than promising that every integration has zero cost.

## Current implementation

Current version: 0.2.0 alpha. Original source: MIT. The unmodified vendored
ICAL.js 2.2.1 is MPL 2.0; retain its license and the third-party notices.

Implemented: day/week/month/agenda views, local events and tasks, calendar
filtering, reviewed quick entry, search, basic drag-to-move, native tray and
reminder code, JSON backup/restore, atomic desktop saves, named event timezones,
DAILY–YEARLY custom rules, RDATE/EXDATE, scoped recurrence edits, ICS metadata and
override preservation, import preview, destination choice, UID deduplication.

Google/Outlook/iCloud account sync, hosted accounts, a production backend,
invitations, and cross-device sync are not implemented. Do not present planned
provider UI as connected accounts. No remote repository was created or published
by the local agent; the user is preparing to upload the source to GitHub.

## Environment setup

Use Node.js 22 or newer and pnpm 11.19.0. At the repository root:

```sh
pnpm install --frozen-lockfile
pnpm test
```

The last local run passed 33 tests. The test command uses Node's in-process test
runner (`--test-isolation=none`) and works without launching Electron. Dependency
download needs access to package registries and Electron's official releases.

For browser UI verification:

```sh
pnpm dev
```

This serves the application at http://127.0.0.1:5173. Use the environment's
supported preview/browser access. The current development server binds only to
loopback. If the environment requires a different host binding, add an explicit
development-only host option rather than assuming the service is public.

Do not use `pnpm start` as a prerequisite for testing the calendar core.
Electron requires a desktop/display environment. Validate actual Windows tray,
notifications, suspend/resume, and native dialogs on a Windows machine or runner.

## Verification and open issues

Read README.md, docs/ROADMAP.md, and docs/VALIDATION.md before changing semantics.
This handoff's web-first direction supersedes Windows-first planning in older
documents. Align the roadmap and product documentation at the next planning pass.
Local browser testing covered migration/reload, timezone creation, custom rules,
one-occurrence editing, this-and-following splitting, and a synthetic ICS import.

The native Electron runtime downloaded and an unpack-and-run Windows ZIP was
built. Native launch was blocked by the managed workspace's Windows ACL, before
the renderer could start. No permissions were changed to bypass it. Actual tray
and notification behavior remain unverified. The local Windows ZIP and browser
data are not source dependencies and should not be committed.

Known recurrence work: DST gaps with COUNT, nominal DURATION semantics, RANGE
exceptions, RDATE periods, multiple RRULEs, whole-series exception rebasing,
and exhaustive provider fixtures. This-and-following currently requires one
RRULE without RDATE. Timed recurrences preserve initial elapsed duration.

## Suggested next task

First reconcile the roadmap with this web-first vision and write a concrete
architecture plan for a hosted, offline-capable calendar. Specify frontend and
backend boundaries, account authentication, durable storage, migrations,
credential handling, background sync, deployment, and recovery. Preserve useful
prototype code and existing data, but evaluate the current frontend and JSON
persistence rather than extending them as the production architecture by default.

Establish a provider-neutral calendar model and finish the recurrence and
recovery work needed for trustworthy sync. Implement Google Calendar, Microsoft
Graph, and CalDAV adapters behind that contract, with an offline outbox,
idempotent writes, explicit conflicts, incremental sync cursors, and actionable
provider errors. Keep credentials out of browser storage, renderer state,
exports, and logs; define secure server-side handling for the web product and
appropriate platform credential stores for native clients.

Real-account testing and OAuth app registrations will require user-supplied
configuration; do not invent credentials. Work toward the month-long
primary-calendar trial before treating the web experience as ready to carry to
the next platform. Add focused semantic tests and end-to-end journey checks.

Preserve existing data and keep documentation and product claims accurate.
