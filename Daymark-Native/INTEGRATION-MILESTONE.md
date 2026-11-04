# First account-integration milestone

The active objective is a rounded native Windows calendar that is ready to begin Google and Microsoft account integration. Continue through this checklist without waiting for another "continue" prompt. Provider authentication and live account changes are the next milestone, rather than a substitute for these foundations.

## Acceptance bar

- Local daily use: month/week/day, search, calendars with names/colors/visibility, event editing, recurring series and single-occurrence changes/cancellations.
- Calendar correctness: explicit instants and source time zones; recurring meetings retain local start times through DST; all-day dates and exclusive ends remain stable; finite and unbounded rules expand only within a requested range.
- Data safety: backwards-compatible loading, preserved migration backups, validated atomic saves, backup/restore, undo/redo including series changes, and no accidental changes to personal data during QA.
- Windows experience: usable light/dark/system themes, keyboard workflows, compact-window layouts, clear validation and accessible names. Verify the actual native app with the isolated test profile.
- Integration boundary: stable calendar/event/series/occurrence identity, explicit supported recurrence semantics, provider-independent sync planning and conflict/deletion/retry tests. Define secure credentials and authorization boundaries before connecting accounts.
- Evidence: automated checks, Windows package CI, native UI QA and a written readiness assessment with remaining limitations.

Reminders, full iCalendar interoperability, advanced scheduling and signed distribution remain important product work. They need not delay beginning account integration once the event model and local workflows meet this bar. Do not claim paid-calendar feature parity or production-grade sync at this milestone.
