# Windows first

Daymark aims to replace a paid everyday calendar, with the speed, reliability and platform integration of a native app. Windows is the active product. The web/Electron prototype is reference material; web-first development is no longer the roadmap.

## Architecture decision

Use C# / WPF for Windows. Keep calendar rules, storage, recurrence, timezone handling, search and provider contracts in portable libraries without WPF dependencies. Keep window management, accessibility, notifications, credentials and lifecycle in the Windows client. Reuse behavior and test fixtures across future clients; do not promise the same UI code on every platform.

GitHub is the source of truth for cloud and local development. Work on branches and review changes through pull requests. Cloud runs automated checks and builds; local Windows verifies the actual WPF application. About shows the version and source commit. Each CI build has a commit-named downloadable artifact. Shipping updates to users will need separate signing, installers, update delivery and platform testing; committing code does not update installed apps.

## Delivery order

1. **Windows foundation (current):** reliable local event editing and backup, keyboard/accessibility checks, identifiable builds, isolated QA calendar, measured rendering and idle resource usage.
2. **Daily-use calendar:** day/week views, recurrence with exceptions, explicit timezones and daylight-saving tests, dependable Windows reminders including restart/sleep recovery, undo/recovery and data migration.
3. **Accounts:** Google, Outlook/Microsoft 365, then iCloud/CalDAV. Design offline edits, conflict handling, incremental sync, secure credentials and cancellation semantics before calling sync complete. Test provider behavior with real accounts when authorized.
4. **Complete workflow:** natural-language entry, calendar sets, tasks, invitations, availability and scheduling, conferencing, tray/quick entry, richer search, and settings. Prioritize reliability and common workflows over a feature-count target.
5. **Distribution:** signed installer, tested automatic updates and rollback, crash diagnostics with user control, migration compatibility and release checklist.
6. **Other platforms:** select native UI per platform when development starts. iOS and Apple Watch need Apple development/signing tools and hardware access. Android, Linux and possibly macOS follow demonstrated Windows quality and available capacity. Reassess portable implementation choices then.

Do not describe legacy floating events as anchored instants, dispatched reminders as observed Windows notifications, or the Electron app as the native WPF client. Feature parity with Fantastical is a long-term product target, not the current preview's capability.

## Active milestone and next phase

The current acceptance bar is [account-integration readiness](INTEGRATION-MILESTONE.md). Native calendars, recurrence with exceptions, timezone semantics, session history, themes and view navigation are implemented locally. The shared core has three-way conflict planning, scoped paginated checkpoints and retry/write-precondition fixtures; version 0.9 adds provider-library OAuth, manual read-only HTTP snapshots and a transactional SQLite cache. Registered OAuth apps and live end-to-end validation remain required. Durable write outbox, conditional writes, conflict UI, delta sync and automatic scheduling are not implemented yet.

Version 0.10 adds a reviewed import flow for Fantastical/Thunderbird/classic Outlook identity metadata, compatible appearance/default-duration preferences and supported .ics exports. Imported accounts are sign-in drafts, not active connections. Private databases and unsupported recurrence patterns require export or later work; see [migration scope](docs/IMPORT.md). It does not remove the OAuth-registration or live-provider QA requirements.

Once native QA and Windows package checks meet that bar, begin Google and Microsoft authorization and read-only calendar discovery, then implement guarded writes. Follow the [provider integration plan](docs/PROVIDER_INTEGRATION.md). Reminder delivery still needs its own Windows lifecycle design and observed banner verification. Do not delay beginning account integration for every remaining paid-calendar feature, and do not claim sync is complete from the planning tests.
