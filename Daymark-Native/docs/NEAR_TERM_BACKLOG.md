# Daymark Windows backlog

Daymark’s next milestone is a dependable Windows calendar for everyday planning. The local preview now covers editing, recurrence, reminders, Quick Add, calendar sets, Agenda, tasks, time blocks, and `.ics` transfer. The connected milestone needs verified Google and Microsoft sign-in, safe writes, and recovery when devices go offline or both sides change an event.

This backlog uses three priorities. **P0** unblocks a dependable preview or safe account use. **P1** rounds out everyday workflows. **P2** follows once those workflows are reliable. Each checkbox describes a result we can verify. Work in order within a dependency chain and group related items into small reviewable releases.

## Next three increments

1. **A preview people can install.** Sign a commit-specific Windows build, test a clean Windows install, and document how to update and roll back. Run the daily-use script on a real PC with keyboard, display scaling, sleep and restart. Start with release items 1–5 and Windows items 36–40.
2. **Connected calendars people can trust.** Verify real Google and Microsoft accounts using disposable calendars, clarify freshness and account recovery, and add incremental reads before enabling writes. Start with connection items 9–14. The write milestone uses items 15–20 in that order.
3. **A smoother planning loop.** Improve rescheduling, add reusable task lists and duration estimates, and help users find time for their tasks. Start with planning items 24–30 and workflow items 41–45.

## P0 · Preview distribution and trust

- [ ] **1. Signed Windows preview.** Publish a build whose Authenticode signature verifies and that launches on a clean Windows machine with Smart App Control enabled. Track the chosen signing route and its prerequisites in the release checklist.
- [ ] **2. Installer and portable upgrade.** Let an existing user upgrade without replacing the local calendar, OAuth cache, calendar sets, or reminder state. Test uninstall and reinstall with a disposable profile.
- [ ] **3. Verified downloads.** Put the version, commit, signature status, and checksums alongside the download. Confirm that the release tag and CI artifact refer to the same source tree.
- [ ] **4. Tested rollback.** Run an update, simulate failure, and recover the previous binary and its compatible data. Show where to find the preserved backup when a data schema changes.
- [ ] **5. Five-minute first run.** Offer a clear choice between starting locally, importing calendars, or connecting an account. A new user can create their first event without signing in.
- [ ] **6. Recovery screen.** Detect damaged data or a failed write and present the last readable backup, path, and a retry or restore action. Opening the screen leaves the original file intact.
- [ ] **7. Versioned whole-profile backup.** Include local calendar data, tasks, named sets and optional preferences in a reviewed archive. Keep tokens and credential files outside it. Test restore into an empty profile and explicit replacement of an existing one.
- [ ] **8. Support diagnostics.** Generate a local diagnostic bundle with explicit preview and redaction. Include the app version, failing operation, and sync status without event titles, email addresses, or secrets by default.

## P0 · Connected calendar foundations

- [ ] **9. Live Google sign-in QA.** Connect a disposable account, select calendars, refresh, restart, and revoke consent. Record exactly what a user sees at each step.
- [ ] **10. Live Microsoft sign-in QA.** Test a personal Outlook account and a work or school account where policy permits. Record how administrative consent, multiple accounts, and expired sessions recover.
- [ ] **11. Account and calendar status.** Show last successful refresh, coverage dates, errors, and the next useful action per account. Keep cached events visible through a failed refresh.
- [ ] **12. Targeted refresh.** Let users refresh one account or selected calendars, cancel work, and see progress. A cancellation keeps the previous complete cache visible.
- [ ] **13. Incremental reads.** Persist provider cursors transactionally. A cursor reset, failed page, or disconnected account cannot turn missing pages into deleted events.
- [ ] **14. Optional background refresh.** Respect metered connections, sleep and battery. Use bounded retries and provider backoff. Let users choose manual mode and inspect what refreshed.
- [ ] **15. Persistent write queue.** Journal each authorized create, edit and delete before sending it. Restart midway through a batch and prove the queue can resume without losing work.
- [ ] **16. Idempotent event creation.** Use stable operation identity. Simulate timeout after remote creation and confirm that retry creates exactly one event.
- [ ] **17. Conditional updates.** Send the observed provider version with a write. Preserve both values when the provider reports that someone edited the event first.
- [ ] **18. Conflict review.** Show local and remote values side by side, including dates, recurrence, and calendar. Support keeping either value or making a separate copy where appropriate.
- [ ] **19. Safe deletes and calendar moves.** Confirm which event or occurrence will change on the provider. Retry or undo has a documented outcome when the remote side has already applied a deletion or move.
- [ ] **20. Series round trips.** Verify repeating rules, single-occurrence moves, cancellations, and supported series edits on both providers. Keep unrepresentable fields intact and expose what needs attention.
- [ ] **21. iCloud and CalDAV.** Add per-server discovery and authentication with explicit account setup. Verify all-day events, time zones, conditional writes, and credential removal on a live test server.
- [ ] **22. Shared calendars and roles.** Surface read, write, and organizer permissions correctly. Refresh permission changes and handle removed calendars without leaving writable controls behind.
- [ ] **23. Calendar-specific notifications.** Let users opt into local reminders for connected calendars with stable occurrence identity. Test provider alert settings, duplicate suppression, and offline behavior.

## P1 · Tasks and time planning

- [ ] **24. Named task lists.** Organize tasks by project or context, move tasks between lists, and filter them independently of calendars. Preserve existing tasks in a default list on migration.
- [ ] **25. Task lists in calendar sets.** Pick the task lists visible in each set, with a clear default for new tasks. Changing sets preserves hidden tasks and their time blocks.
- [ ] **26. Agenda and task pane.** Offer a compact, persistent task pane beside the timeline. Make keyboard movement between date navigation, calendar items and tasks predictable.
- [ ] **27. Drag tasks to schedule.** Drag an open task onto Day or Week and review the proposed start, duration, and calendar. The keyboard Schedule action should produce the same result.
- [ ] **28. Estimates and planned time.** Give tasks a preferred duration and make the chosen estimate the default for a new block. Keep deadline and scheduled start editable independently.
- [ ] **29. Multiple work sessions.** Allow a task to reserve several time blocks. Completion leaves a clear choice for keeping or cancelling future blocks. Undo restores the entire choice.
- [ ] **30. Free time finder.** Suggest slots that fit a task’s estimate inside working hours. Let users choose which calendars count as busy and review a slot before booking it.
- [ ] **31. Recurring tasks.** Define whether recurrence follows the due date or completion date. Completing one occurrence should preserve the rule and its future instances.
- [ ] **32. Task reminders.** Add date and time reminders with snooze and completion cancellation. Verify behavior if a task is rescheduled, completed or reopened while an alert is pending.
- [ ] **33. Subtasks and checklists.** Create lightweight steps with keyboard add, reorder, completion, and undo. Show progress without auto-completing the parent unexpectedly.
- [ ] **34. Task provider pilots.** Integrate a test Google Tasks or Microsoft To Do list behind opt-in after calendar writes are reliable. Define field mapping and conflict behavior first.
- [ ] **35. Focus planning.** Add a lightweight daily review for overdue tasks, unscheduled important work, and upcoming deadlines. Keep it driven by local data with an explicit user action.

## P0/P1 · Windows reliability and polish

- [ ] **36. Device QA script.** On a supported Windows PC, create and edit a recurring event, move one instance, schedule a task, switch sets, and restore a backup. Record the version and observed outcomes.
- [ ] **37. Keyboard and screen readers.** Exercise the same script with Tab, arrows, shortcuts, Narrator, and high contrast. Fix unnamed controls, focus loss, and lists that trap navigation.
- [ ] **38. Small and large displays.** Verify 125–200% scaling, a small laptop window, and two monitors with different scaling. Keep toolbars, dialogs, and the tray reminder window reachable.
- [ ] **39. Native notification delivery.** Send alerts through the supported Windows notification path after signing is ready. Verify Notification Center, Focus Assist, clicks, dismissals, and the fallback when notifications are disabled.
- [ ] **40. Startup and wake recovery.** Offer opt-in launch at sign-in, handle resume and device clock changes, and verify no duplicate reminders after a restart. Show how to disable background operation.
- [ ] **41. Drag resize and all-day moves.** Change duration by dragging an edge, move all-day dates directly, and keep a keyboard equivalent. Test overnight events and daylight saving transitions.
- [ ] **42. Fast event actions.** Provide a consistent context menu with duplicate, move, copy details, delete, and edit scope. Connect the same commands to Agenda, the schedule list, and the timeline.
- [ ] **43. Preview inspector.** Show details and actions when an event is selected, with explicit Edit for changes. Make long notes, locations, and conferencing links easy to reach.
- [ ] **44. Better date navigation.** Add adjustable week start, Work Week, a configurable multi-day view, and a year overview. Preserve the selected day when switching views and respect date boundaries.
- [ ] **45. Time controls.** Add a current-time marker, optional second time zone, configurable working hours, and a 12/24-hour choice. Verify display and editing across midnight and daylight saving changes.
- [ ] **46. Search filters.** Add date range, calendar, and text fields with keyboard selection and clear match counts. Keep recurring searches bounded and make their window visible.
- [ ] **47. Remember workspace layout.** Restore view, date, scroll, panel width, and window bounds. Clamp restored bounds to an available display when a monitor is removed.
- [ ] **48. Larger calendars.** Profile startup, Agenda, scroll, search, and month navigation with representative synthetic histories and recurring events. Set measured budgets and catch regressions in CI.
- [ ] **49. Appointment defaults.** Let users choose default duration, calendar, event alert, all-day alert, and default meeting visibility. Preserve imported preferences and show which default is active.
- [ ] **50. Quick Add expansion.** Add `in 2 weeks`, more date formats, date ranges, location, and localized grammar. Make ambiguous phrases explicit in the review step.
- [ ] **51. Global entry and tray agenda.** Offer an opt-in shortcut and a compact upcoming list from the tray. Avoid shortcut conflicts and ensure Quit releases the shortcut.
- [ ] **52. High-contrast and dark polish.** Verify date pickers, list selections, hover, errors, and appointment colors under each theme. Fix clipping and low-contrast combinations with fixtures.

## P1/P2 · Exchange, scheduling and differentiation

- [ ] **53. Live `.ics` subscriptions.** Add a refreshable read-only calendar URL with clear update status. Handle TLS, redirects, untrusted URLs, payload limits and stale results.
- [ ] **54. Complete iCalendar export.** Export repeat rules, exceptions, source time zones and supported alarms. Round-trip representative files through another calendar and document deliberate conversions.
- [ ] **55. Import reconciliation.** Preview duplicates, changed events, unsupported recurrence and task rows before importing. Let users choose whether to add, skip, or update a matching event.
- [ ] **56. Availability and busy controls.** Support free, busy, tentative, and out-of-office where providers allow. Verify how those states affect free time suggestions and invitation replies.
- [ ] **57. Invitations and RSVP.** Review organizers and attendees, update responses, and show delivery outcomes. Use test mailboxes so invites and cancellations go only to intended recipients.
- [ ] **58. Meeting links and locations.** Recognize common conferencing links, open them explicitly, and preserve location text. Add provider-native meeting creation only after permission and invitation handling are tested.
- [ ] **59. Time-zone scheduling aid.** Show local times for selected cities while proposing a meeting. Clearly mark date changes and outside-working-hours options.
- [ ] **60. Share availability.** Copy a reviewed list of free slots with time zone and date. Explore booking links after explicit privacy controls and provider write recovery are ready.
- [ ] **61. Templates and calendar automation.** Save reusable appointments and optional calendar set schedules. Preview each rule, explain which rule selected the active set, and allow one-click override.
- [ ] **62. Platform handoff.** Reuse validated core fixtures and backup formats in the Linux client. Define shared sync contracts before planning macOS or mobile clients, with release and hardware requirements per platform.

## Readiness checks

- **Reasonable local option.** A user can install the app on their Windows machine, import or create a calendar, get a reminder after sleep, plan a week with tasks and sets, recover an accidental change, and restore a backup. Verify the complete flow on a real device.
- **Reasonable connected option.** A user can connect and refresh a real account, edit while offline, reconnect without duplicate events, and resolve an event changed on both sides. Run this for Google and Microsoft with disposable calendars and repeat it across an update.
- **Release evidence.** Record exact builds, tests, manual observations, known gaps and a recovery path. Use versioned preview artifacts while improving the app through reviewable pull requests.

Calendar sets, task planning and schedule views have established user workflows in existing products. These references informed the scope of the planning work. [Fantastical calendar sets](https://flexibits.com/fantastical-windows/help/calendar-sets) · [Outlook task scheduling](https://support.microsoft.com/en-us/outlook/calendar/drag-a-task-to-your-calendar-with-to-do-in-outlook) · [Google Calendar views](https://support.google.com/calendar/answer/6110849)
