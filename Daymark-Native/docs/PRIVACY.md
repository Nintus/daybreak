# Privacy policy — native Windows preview

This policy describes `Daymark-Native`, the C# / WPF client. It does not describe the older web/Electron prototype or planned connected-account features.

## Calendar data

The current client stores event titles, dates/times, locations and notes locally in `%LOCALAPPDATA%\DaymarkNative\calendar.json`. The previous successful save is `calendar.json.bak`. The optional `--test-calendar` profile instead stores synthetic QA data in `TestCalendar` beside the executable. These files are not encrypted by Daymark; access depends on your Windows account and device protections.

The native client currently implements no analytics, advertising, automatic crash uploads, account connection, background provider sync or automatic updater. Its current application code makes no network requests. Proposed account and sync features require a policy update before release.

## Backups and sharing

Save backup writes a complete JSON calendar to the location you choose. Restore reads a chosen file and asks before replacing current calendar data. A location inside a cloud-synced folder may be uploaded by that folder's service; Daymark does not control that service. Backup files contain event details, so review them before sharing.

When reporting a problem, use synthetic events or redact calendar details and personal paths from screenshots, logs and backups. The app does not automatically send issue reports. Information you choose to post on GitHub is handled under [GitHub's privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

## Builds and removal

Downloading releases and obtaining .NET development dependencies involve GitHub or Microsoft's services, separately from running the offline calendar. The packaged .NET runtime and Windows remain subject to their own terms and settings. No signing-provider service is currently called by the app; any future build-signing integration operates on software artifacts, not users' calendars.

The portable preview installs no background service or startup registration. To remove it, close Daymark and remove its extracted app folder. Personal calendar data remains in the location above; make a backup before removing that data separately. Test-calendar data lives inside its build folder and should be backed up before that folder is removed.
