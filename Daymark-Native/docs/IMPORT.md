# Bring another calendar into Daymark

Open **File → Import from another app…**. Daymark scans supported local metadata and shows a preview. Check the identities and files you want, optionally choose compatible settings, then select **Import selected**. Scanning never saves profiles, changes preferences or connects to an account. **Close** leaves an unimported preview behind.

## Supported sources

| Source | Account discovery | Settings | Calendar data |
| --- | --- | --- | --- |
| Fantastical for Windows | `Database/Sources-*.plist` metadata under its roaming app folder; newest version first | Whole-minute default event duration; standard light/dark theme when explicitly selected for both appearance modes | App-folder `.ics` files or exports you select; private `.fcdata` database is not converted |
| Classic Outlook | Current-user Office 15/16 profile account metadata, including string and Unicode-binary email fields | Outlook preferences are not converted | Select exported `.ics` calendars; PST/OST files are not converted |
| New Outlook | No private cache reader; detected installation is explained in preview | Not converted | Select an `.ics` export or connect the account |
| Thunderbird | Registered/default local profiles: calendar usernames/server origins and mail identities from `prefs.js` | Standard light/dark theme and default event duration | App-profile `.ics` files and local file-based calendar preferences; `local.sqlite` calendars need export |

Discovery is best effort: source formats can change. It does not run scripts, launch Outlook, query the network, access credential managers, read password databases or copy OAuth tokens/client registrations. A mail identity may not have a calendar. Remote subscriptions are not downloaded and their URLs are not copied; URL paths, queries and embedded credentials are removed from saved server information. Automatic file search is limited to known app/profile folders, three levels, 1,000 folders and 100 files. It does not search your drives or Downloads. **Add .ics files…** handles exports saved elsewhere. UNC paths and directory links are excluded from automatic discovery.

## Sign back in

Imported profiles are listed under **File → Calendar accounts** as **Sign-in required**, even before a provider is connected. Google and Microsoft sign-in buttons supply the discovered email as a login hint, so you do not have to type it again. For an unknown provider, choose Google or Microsoft yourself. A provider can still ask you to choose an account; check the identity on its consent screen. Daymark lists the identity actually connected and retains a mismatched draft for another attempt. Remove profile removes only the imported draft.

Daymark's own [OAuth application setup](ACCOUNT_SETUP.md) is still required. Another app's client IDs and credentials cannot substitute for it. iCloud and other CalDAV profiles are retained with a clear pending-support message; connecting those providers is not implemented. Current Google/Microsoft sync remains manual and read-only.

Profiles are saved in `imported-accounts.json` beside your native calendar and remain after restart. Identity matching is based on provider and login (plus server origin for generic servers), not event titles. Known provider identities are deduplicated across apps; an unspecified mail identity can merge with a uniquely identified provider profile. At most 200 imported profiles are retained. No credentials are stored in that file. See [privacy](PRIVACY.md).

## Calendar exports

Use the original app's calendar export command, save an **iCalendar / .ics** file, then choose **Add .ics files…**. For Thunderbird local calendars, use **Events and Tasks → Export**. In classic Outlook, use **File → Save Calendar** and choose the intended date range and detail level; an export can include only the range/options you chose. Fantastical local calendars also need an .ics export; remote calendars can be retrieved after account sign-in. If an app/version has no export command for a calendar, use its provider's export tools.

The importer accepts iCalendar 2.0 calendar exports, UTF-8 text, folded lines, title, location, description, floating times, UTC, installed IANA/Windows time-zone IDs, exclusive all-day end dates and positive durations. Supported recurrence is daily, weekly weekdays, monthly date/ordinal/last weekday, and yearly date, with an interval and count or inclusive end. EXDATE exclusions and RECURRENCE-ID moved/cancelled occurrences are kept as native exceptions. UNTIL timestamps are converted into the series' source zone before applying its end date.

Unsupported recurrence constraints (including RDATE/EXRULE, hourly rules, multiple monthly dates and THISANDFUTURE), unresolvable/ambiguous seed times, duplicate revisions and invalid exceptions skip the **entire event series**, with a notice. Unknown custom VTIMEZONE definitions are not converted; installed zone rules are used. Zero-duration timed events and zoned durations expressed in calendar days/weeks are not represented. Tasks/journals, alarm delivery, invitations/attendees, attachments, privacy and availability settings are not transferred. The preview explains these losses; keep the original export. Do not use this importer to process meeting request or cancellation messages.

Each file is limited to 20 MB, 10,000 VEVENT/imported records and 1,000 occurrence changes per series; unfolded lines are limited to 100 KB. The resulting local document must fit Daymark's existing 100-calendar, 100,000-record and 20 MB limits. A skipped group does not prevent other supported groups from being reviewed. The preview shows event/series and exception counts separately.

Calendar identity uses its full exported `X-WR-CALNAME`, or filename when absent, plus each event's UID. Reimporting the same named calendar preserves existing events and local edits, including whole recurring series; it does not update them from a changed export. Changing an unnamed file's name gives it a different calendar identity. Separate source calendars with the same name and UID are treated as the same item. Importing an export as well as connecting its remote calendar can display duplicates: choose one source for remote data.

## Saves and recovery

Selected event files are merged as one normal calendar save, with session **Undo/Redo** and the existing previous-save backup. Existing events are retained. Account drafts and preferences are separate durable saves: if a later category fails, the dialog names completed categories, retains their results and supports retry without duplicating them. Calendar Undo affects event imports, not identities or settings. Settings default to **Keep my current…**; conflicting source settings are choices, never silently combined.

An imported default duration is stored in `default-event-minutes.txt` (1–1,440 minutes) and applies to newly created timed events. Existing events keep their duration. `appearance.txt` stores your selected Daymark theme; proprietary theme files, week-start and notification settings are not converted.

Implementation references: [iCalendar RFC 5545](https://www.rfc-editor.org/rfc/rfc5545), [Thunderbird calendar preference/storage overview](https://source-docs.thunderbird.net/en/latest/calendar/calendars.html), [Microsoft calendar export API](https://learn.microsoft.com/en-us/office/client-developer/outlook/pia/how-to-save-a-calendar-to-disk). Local app metadata readers are compatibility adapters, not vendor-supported migration contracts.
