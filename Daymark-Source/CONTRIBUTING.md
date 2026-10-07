# Contributing

Daymark is an early MIT-licensed project. Small, reviewable contributions are
welcome. Start with the roadmap and include a concrete before/after behavior.

Run `pnpm test` for calendar changes. Include boundary tests for recurrence,
all-day dates, DST, imports, or persistence when those semantics change.
For UI changes, verify day/week/month/agenda views, both themes, and keyboard
navigation. Test desktop behavior separately from the browser preview.

Use sample data or disposable test accounts. Do not commit tokens, calendar
exports containing personal information, app-specific passwords, or local backups.
Provider credentials belong in the main process's secure store once implemented.

Avoid pretending an unimplemented provider is connected. Keep app copy and
README limitations consistent with actual behavior. New dependencies need a
license check and a reason that is stronger than convenience alone.
