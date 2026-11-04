# Account-integration readiness QA — November 3, 2026

Client: native C# / WPF, version 0.8.0. Data: isolated `--test-calendar` profile. No personal calendar, provider account, Windows security setting or Windows timezone setting was changed.

## Automated evidence

The core test harness covers recurrence intervals/count/until, multiple weekdays, monthly ordinal/last-weekday dates, leap dates, DST gaps/folds, all-day and overnight overlap, moved/cancelled exceptions, series resets, calendar identity, migration backups, undo/history snapshots and dense bounded expansion. Sync-planning fixtures cover edit/edit and edit/delete conflicts, conditional versions, scoped pagination, failed checkpoint persistence, retry limits and expired cursors. These are not live provider tests.

## Native interaction evidence

- Loaded the existing version-2 QA calendar without rewriting it. The first successful edit saved version 3 and preserved `calendar.json.v2.bak` alongside the existing `.v1.bak` and rotating `.bak`.
- Created “Readiness QA · Weekly planning” at 09:00–10:00 Chicago, every Tuesday through December 15. Month view expanded the weekly occurrences. The saved rule retained the source timezone and inclusive end date.
- Opened November 10 and chose **This occurrence**. The editor opened that date in the source zone, kept the calendar picker disabled for the occurrence, and retained its parent identity. Moved it to November 11; November 10 became empty and November 11 showed the replacement. JSON retained `OriginalStart = 2026-11-10T09:00:00` and the parent series ID.
- Added a teal work calendar through Manage calendars. Opened the recurring event again, chose **Entire series**, and reassigned it to that calendar. The master and moved exception both acquired the new calendar ID; the November 11 move remained intact.
- Hid the personal calendar. Only work-calendar events remained visible. The SHA-256 of `calendar.json` stayed `f2efc6574d434073b52a3e26d2f0dddd2f3983d58cfdd6c50dcc8c2314e29231`; visibility was stored separately as `["personal"]`.

## Rebuilt 0.8.0 verification

- Built against GitHub commit `e40db5b822e43d9c40faa4754c1a5aaa25510c61`; executable product version reports `0.8.0+e40db5b822e43d9c40faa4754c1a5aaa25510c61`.
- Restart retained the migrated calendar, work-calendar color, moved occurrence, dark preference and hidden-personal-calendar filter.
- Ctrl+G opened Go to Date; selecting November 11 showed the saved replacement. Ctrl+F focused search. Searching “Readiness QA” returned seven dated occurrences with the moved November 11 result and an explicit repeat-search window.
- New event defaulted to the visible work calendar. Expanding weekly controls exposed an auto-sizing dialog growth issue; the follow-up fixes the form to a resizable, screen-bounded viewport with a separate action footer. Final verification of that correction is in progress.

## Verification still required before this report is final

Final viewport and invalid-date-input checks, updated package CI and an idle-resource sample are being completed. High contrast, alternate DPI, screen readers, real provider fixtures, credential storage, transport and durable transactional sync remain outside the results above. UI deletion/cancellation was not executed; those semantics are covered by core tests.
