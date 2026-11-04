# CPU efficiency and first provider transport — November 3, 2026

## Implementation

- Unchanged activation retains the calendar visual tree. Device-zone/day changes still refresh it. Theme refresh no longer replaces unchanged resource entries, broadcasts unchanged colors or repeats title-bar notifications. No recurring background work or provider polling is added.
- Google PKCE/system-browser authorization through Google's library; Microsoft public-client/system-browser authorization through MSAL. DPAPI current-user protection for application settings and token caches. No passwords are collected and no credentials enter local JSON backups.
- Read-only calendar discovery and bounded full snapshot downloads through actual Google Calendar and Microsoft Graph HTTP endpoints. Complete pagination, continuation host/path validation, request cancellation and bounded response sizes. Graph requests immutable IDs and UTC responses.
- Account selection, all selected calendar snapshots and coverage commit in one SQLite transaction. Failure retains the previous cache. Provider data is separate from local edits, history and export. Unknown raw provider fields are retained; provider-expanded instances are not editable local series.
- Native account setup/selection/refresh/disconnect UI and read-only provider-event details. See [account setup](docs/ACCOUNT_SETUP.md).

## Local evidence

The core/provider harness passed **109 checks**. The native app and Windows-specific checks compile with zero warnings/errors. Tests cover mapping UTC/source-zone/all-day times, opaque account identity, pagination/cycles, hostile continuation links, cancellation, offline reopen, failed transaction rollback and full-snapshot deletion replacement.

A pre-change unrestricted-launch sample reported 0.453125 CPU seconds over 20.0101909 seconds (~2.26% of one core), 192.47 MiB working set and 125.12 MiB private bytes. A subsequent optimized-build sample reported 9.578125 CPU seconds over 20.0062604 seconds (~47.88%), 261.57 MiB working set and 177.83 MiB private bytes. Automation had attached between runs, so these are **inconsistent, contaminated evidence**, not a demonstrated CPU reduction. The previous thread-time trace showed UI Automation activity and stable app layout/refresh counters; it did not prove CPU attribution.

After resetting automation, an attempted new-build run produced zero-CPU figures, but inspection found **a startup error dialog**: Smart App Control blocked `Daymark.Providers.dll` (`0x800711C7`). Those figures are invalid and excluded. Windows protections were not changed. Local account UI/live sign-in verification is blocked by signing; the new provider assembly cannot currently be loaded in the native executable on this machine.

## Windows CI checks

Eight Windows checks exercise idempotent theme refresh, preservation of month visuals across 100 activations, day-change invalidation, protected configuration round-trip/no plaintext, Google token/expiry round-trip and account-dialog construction. The unattended benchmark validates the exact test-calendar window title before and after sampling, so it cannot report a startup dialog as successful idle behavior. Its results are saved separately from the distributable package.

[The initial Windows run](https://github.com/Nintus/daybreak/actions/runs/41780605677) passed all 108 then-current core/provider checks, seven then-current Windows checks and package validation. Its unattended `0.9.0+a1a10e2` calendar measurements, after 20 seconds warmup, were:

| Sample | Elapsed | Recorded process CPU time | Working set | Private bytes |
| --- | --- | --- | --- | --- |
| 1 | 20.0041 s | 0.0000 s | 115.66 MiB | 44.38 MiB |
| 2 | 20.0014 s | 0.0000 s | 115.61 MiB | 44.29 MiB |

These show no measurable idle CPU within those short windows, not a promise of zero CPU in all circumstances. Hosted runner measurements are synthetic empty-calendar evidence; they do not replace profiling on the target PC, large-calendar UI tests or battery measurements. The final follow-up adds the explicit-offset mapping fixture and protected Google token round-trip check; its CI result is linked from PR #9.

## Not yet validated or enabled

No OAuth applications were registered by the user yet, so real sign-in, token renewal, provider fixtures from disposable calendars, shared-calendar behavior and live remote changes have not been verified. The current feature is manual read-only refresh, **not two-way or incremental sync**. Durable outbox/conditional writes/conflict UI, sleep/resume scheduling, signed distribution and clean target-device performance measurements remain open. Broader DPI/high-contrast/screen-reader QA is also pending.
