# Native week-view verification

November 3, 2026 — actual C# / WPF client running on Windows, with `--test-calendar`. The executable opened for the functional tests below; no security settings were changed.

## Automated

All 21 core/storage tests pass. Seven added checks cover Monday week starts across year/leap-day boundaries, overlapping versus adjacent appointments, overnight clipping with exclusive midnight ends, all-day separation, selectable short events without data changes, and 50-event overlap chains with lane reuse. Release WPF compilation succeeds with zero warnings and zero errors.

## Observed with the computer-use skill

- Switched from Month to Week; the selected date remained November 3.
- Created a timed appointment by double-clicking an empty timeline slot and saving with Enter.
- Created a second appointment using Ctrl+N. The two 09:00–10:00 events appeared side by side and in the selected-day agenda.
- Double-clicked a later Wednesday slot; the editor offered November 4 at 14:30–15:30.
- Used the All day access key and saved that draft; the event appeared in Wednesday's all-day area with November 5 as its exclusive end.
- Next changed the displayed range from November 2–8 to November 9–15 and the selected day to November 11.
- Closed the app, rebuilt, reopened the same test calendar, and verified all three saved events remained.
- Verified the corrected week layout fits the default window without horizontal scrolling, with day headers and timed columns aligned.
- Clicked an existing appointment, changed its title to “Week QA · Design review,” and saved. Both the timeline and agenda updated.

Only synthetic local test events were created. Personal calendar data was not used. The QA screenshot is delivered separately; test data under bin/ is excluded from source control and build artifacts.

## Remaining limits

After the successful functional UI tests, the version was stamped 0.3.0 and the Release build passed again. Reopening that final binary was blocked with “An Application Control policy has blocked this file,” including an approved development launch. The final version-stamped executable's launch is therefore unverified. The screenshot and UI observations describe the immediately preceding build with identical week-view code, before the version stamp.

Arbitrary DPI scaling, screen readers, minimum-size resizing, dense-calendar usability, performance and battery use were not measured. Drag/resizing appointments, configurable week starts and timezone-aware display are not included. Week mode is currently a per-session choice. The Windows build still supports local wall-clock events only; provider sync, recurrence and reminders remain planned.
