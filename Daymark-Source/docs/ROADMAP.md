# Toward a full Windows calendar

The target is a daily-driver app with Google, Outlook, and iCloud support.
The current local alpha is the foundation, not the finished product.

## Milestone 1 — interoperable calendar core

Delivered in 0.2: ICAL.js integration, named event timezones and embedded
VTIMEZONE definitions, advanced DAILY–YEARLY recurrence, RDATE/EXDATE,
RECURRENCE-ID preservation, one/following/series editing, and import preview.
The remaining items below are needed before this milestone is complete.

- Complete recurrence conformance testing, especially DST gaps with COUNT,
  nominal DURATION semantics, RANGE overrides, multiple RRULEs, and RDATE periods.
- Rebase existing exception identities during whole-series date/time changes.
- SQLite migrations, transactions, recoverable deletes, undo, and recovery UI.
- Add actionable diagnostics for unsupported calendar-level properties and VTODO.
- Event resize, move-series choices, task editing, task-to-calendar time blocking.
- Accessibility audit, screen-reader testing, high contrast, zoom, localization.

Acceptance: DST transitions, timezone changes, complex repeats, exceptions, and
provider ICS fixtures survive import/export without semantic loss. No silent
data truncation, unsupported-property loss, or skipped occurrence limits.

## Milestone 2 — Google + Outlook + iCloud two-way sync

Ship all three behind the same provider contract. The renderer remains offline
capable and never receives refresh tokens or account passwords.

| Provider | Transport / authorization | Core work |
| --- | --- | --- |
| Google | Calendar API, system-browser OAuth desktop flow | App registration, consent, refresh lifecycle, calendar discovery, sync tokens, deleted events, etags, shared calendars |
| Outlook / Microsoft 365 | Microsoft Graph, system-browser OAuth with PKCE | App registration, delegated scopes, calendarView delta, pagination, shared calendar rights, recurring series and instances |
| iCloud / generic CalDAV | CalDAV over HTTPS; supported authorization or app-specific credentials | Principal/home discovery, calendar-query, sync-collection, etags, recurrence interoperability, credential revocation |

Provider contract: discover accounts/calendars, pull changes using an opaque
cursor, push create/update/delete with an expected revision, expose capabilities
and read-only status, and normalize errors without leaking credentials.

Use an encrypted credential store backed by Windows facilities. Keep secrets
out of JSON exports, logs, the renderer, and source control. Google/Microsoft
require developer app registrations; public release needs the relevant consent
and verification work. CalDAV interoperability needs real-account testing.

Maintain an offline outbox, idempotent operation IDs, explicit conflict records,
retry/backoff, and a last-successful-sync indicator. Never overwrite a provider
change blindly. A disconnected account should preserve locally cached data
until the user chooses what to remove.

Acceptance: create/edit/delete offline and reconnect against all three test
accounts without duplicates or lost changes; handle concurrent edits, revoked
credentials, rate limits, permissions, and cancellations visibly.

## Milestone 3 — scheduling and collaboration

- Attendees, organizer identity, invitations, RSVP, cancellation, availability.
- Conference links using provider-supported integrations.
- Multiple timezone display, travel timezone support, working hours, buffers.
- Meeting proposals, booking links, and availability sharing (optional service).
- Task provider integrations, recurring tasks, focus blocks, calendar sets.
- Better natural-language grammar, live parse preview, accessible command palette.
- Year view, customizable week length, print/export, subscribed calendar refresh.

## Milestone 4 — dependable Windows release

- Signed installer, custom executable branding, update channels, rollback.
- Optional startup, global quick-entry shortcut, tray agenda, reminder snooze.
- Reminder scheduling that survives renderer suspension; explicit missed alerts.
- Windows 11 x64/ARM64 verification, suspend/resume and system-time-change tests.
- CI, reproducible release instructions, license inventory, dependency updates.

## Reference sources

Feature inspiration: [Fantastical](https://flexibits.com/fantastical-windows/help),
[Morgen](https://www.morgen.so/), [Calendar 366](https://calendar366.com/).
Daymark uses an original interface and implementation.

Integration references: [Google Calendar authorization](https://developers.google.com/workspace/calendar/api/auth),
[Microsoft Graph calendar overview](https://learn.microsoft.com/en-us/graph/outlook-calendar-concept-overview),
[Apple third-party app credentials](https://support.apple.com/en-us/102654),
[Electron security](https://www.electronjs.org/docs/latest/tutorial/security).
