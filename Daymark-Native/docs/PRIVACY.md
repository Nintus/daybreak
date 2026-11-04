# Privacy policy — native Windows preview

This policy describes `Daymark-Native` version 0.9, the C# / WPF client, including optional Google and Microsoft read-only connections. It does not describe the older web/Electron prototype.

## Calendar data

The current client stores event titles, dates/times, timezones/UTC instants for anchored timed events, locations and notes locally in `%LOCALAPPDATA%\DaymarkNative\calendar.json`. The previous successful save is `calendar.json.bak`. Migration from version 1 also preserves `calendar.json.v1.bak`. The optional `--test-calendar` profile instead stores synthetic QA data in `TestCalendar` beside the executable. These files are not encrypted by Daymark; access depends on your Windows account and device protections.

The native client implements no analytics, advertising, automatic crash uploads or automatic updater. Local calendar use does not require network access. Connecting an account opens your system browser for Google or Microsoft authorization. Manual refresh sends authenticated calendar-list and event queries to that provider. Authentication libraries also contact provider token endpoints when obtaining or refreshing credentials. Daymark does not send calendar data to a Daymark server.

## Connected accounts

Connection metadata, selected calendars, date coverage, downloaded events and original provider payloads are stored locally in `accounts.sqlite` beside your local calendar. Provider payloads can include attendee, organizer, meeting link, invitation and description details beyond the displayed projection. This cache is not encrypted by Daymark; access depends on Windows/device protections. It is separate from local event backups and undo history. Connected events are read-only in this preview; Daymark makes no calendar-write API requests.

OAuth application settings and token caches live in the `credentials` subfolder, protected with Windows DPAPI for the current Windows user. They are not included in JSON backup exports or application packages. Never share that directory. Your account password is entered on the provider's sign-in page and is not collected by Daymark. Provider authorization, consent and account policies are handled by Google or Microsoft.

Refresh is manual, covers a bounded date range and retains the previous cache on failure. There is no periodic polling or background service. Disconnect removes that connection's local cache and saved tokens; it does not remove server calendar events or revoke the provider's authorization grant. Authorization can be revoked through the provider's account controls. See [account setup and sync behavior](ACCOUNT_SETUP.md).

## Backups and sharing

Save backup writes a complete JSON calendar to the location you choose. Restore reads a chosen file and asks before replacing current calendar data. A location inside a cloud-synced folder may be uploaded by that folder's service; Daymark does not control that service. Backup files contain event details, so review them before sharing.

When reporting a problem, use synthetic events or redact calendar details and personal paths from screenshots, logs and backups. The app does not automatically send issue reports. Information you choose to post on GitHub is handled under [GitHub's privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

## Builds and removal

Downloading releases and obtaining .NET development dependencies involve GitHub or Microsoft's services, separately from running the offline calendar. The packaged .NET runtime and Windows remain subject to their own terms and settings. No signing-provider service is currently called by the app; any future build-signing integration operates on software artifacts, not users' calendars.

The portable preview installs no background service or startup registration. To remove it, close Daymark and remove its extracted app folder. Personal calendar data remains in the location above; make a backup before removing that data separately. Test-calendar data lives inside its build folder and should be backed up before that folder is removed.
