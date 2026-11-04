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
- New event defaulted to the visible work calendar. Expanding weekly controls exposed an auto-sizing dialog growth issue. The follow-up fixes the form to a resizable, screen-bounded viewport with a separate action footer. In the rebuilt `e433152` executable, Save/Cancel remained visible with weekly options expanded.

## Final input and package checks

- Invalid typed start-date text cleared the selected date. Save kept the editor open, scrolled to a clear validation error and did not modify the event file. Escape discarded the unsaved draft.
- The core harness passed **93 checks**. The Release WPF build reported zero warnings/errors. Both Windows CI runs for `e433152c5b06d0198e30dbfe899aca61f0b7f857` passed, including the updated package contents checks.

## Resource investigation and limits

Two launcher-handle samples of `e433152` with six stored event records reported:

| Warmup | Sample | CPU time | CPU relative to one core | Working set | Private bytes |
| --- | --- | --- | --- | --- | --- |
| 15 s | 10.007 s | 2.8281 s | 28.263% | 209.03 MiB | 136.05 MiB |
| 30 s | 30.008 s | 10.7031 s | 35.668% | 208.05 MiB | 133.76 MiB |

These are concerning measurements from an automation-attached session, **not an accepted idle baseline**. A temporary diagnostic build showed only two calendar refreshes and stable layout counts while process CPU continued rising. A 45-second Microsoft dotnet-trace sample showed substantial UI Automation provider/dispatcher activity (`ElementUtil.Invoke`, `ElementProxy` and dispatcher waits); it does not establish how much CPU belongs to automation versus ordinary use. The trace samples thread time and includes waits, so its percentages must not be read as CPU attribution.

The temporary logging code was removed. Accessibility was not disabled to improve a benchmark. Obtain an unattended baseline without automation attached before making resource-efficiency or battery claims. Large-calendar UI responsiveness, alternate display scaling and background lifecycle also need dedicated measurements. Dense recurrence expansion is covered by core fixtures; that is not a UI benchmark.

High contrast, alternate DPI, screen readers, real provider fixtures, credential storage, transport and durable transactional sync remain outside the observed results. UI deletion/cancellation was not executed; those semantics are covered by core tests.

## Readiness assessment

**Ready to begin Google and Microsoft account integration.** Local calendar identity, recurrence and exceptions, source time zones, calendar management/filtering, keyboard navigation, backups/migration and the tested provider-independent merge boundary are sufficient foundations for the next phase. Native interaction checks used isolated test data and verified persistence across restarts.

Start with user-authorized sign-in and read-only calendar discovery, then a durable transactional sync store and read-only event synchronization. Conditional writes follow provider fixtures and recovery tests. The interfaces and planning tests in this milestone do not provide working account sync.

The next phase can begin; production release and paid-feature parity still require further work. Reminders, signing/distribution, full iCalendar support, clean resource measurements and broader accessibility/display QA remain tracked product work. See [the provider integration plan](docs/PROVIDER_INTEGRATION.md).