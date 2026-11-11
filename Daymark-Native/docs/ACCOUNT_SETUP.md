# Google and Microsoft account setup

Version 0.9 adds an actual OAuth/HTTP path for **manual, read-only calendar synchronization**. Downloaded events are separate from local events and cannot be edited or deleted in Daymark. Remote changes appear after **File → Calendar accounts → Refresh selected**. Coverage is the past three months and next fifteen months in UTC at refresh time; dates outside that range have not been downloaded. This is not yet two-way sync or incremental delta sync.

The open-source preview has no shared OAuth registrations. Configure your own applications once on this PC. Do not post client JSON, tokens, passwords or screenshots of credentials on GitHub. Codex's connected accounts cannot supply Daymark credentials.

## Google

1. In [Google Cloud Console](https://console.cloud.google.com/), select or create a project and enable **Google Calendar API**.
2. In Google Auth Platform, configure Branding, Audience and Data Access. For a personal development project use the appropriate testing audience and add your Google account as a test user. Request `https://www.googleapis.com/auth/calendar.readonly`; Daymark does not request calendar write access.
3. Create an OAuth client of type **Desktop app**, not Web application, Android or iOS. Download the client JSON and retain it privately.
4. In Daymark open **File → Calendar accounts → Account setup**. Copy `installed.client_id` and `installed.client_secret` from that JSON into the Google fields. Save setup.
5. Choose **Connect Google**. Complete sign-in and consent yourself in the system browser. After discovery, select the calendars you want and choose **Refresh selected**.

Daymark uses Google's .NET authentication library with PKCE and a loopback browser callback. A desktop client secret is not a confidential server secret; desktop applications cannot keep an embedded secret confidential. Google's [desktop OAuth guide](https://developers.google.com/identity/protocols/oauth2/native-app) describes the flow and registration requirements. Testing and organization policies can limit authorization or token lifetime; reconnect when required.

## Microsoft / Outlook

1. Open [Microsoft Entra app registrations](https://entra.microsoft.com/) and create a registration for Daymark development. Choose account support matching your intended users; for Outlook.com and work/school accounts, select organizational directories plus personal Microsoft accounts. Tenant administrators may restrict registration or consent.
2. Under Authentication add **Mobile and desktop applications** and the redirect URI **`http://localhost`** for the system-browser flow. Do not create or embed a client secret.
3. Configure delegated Microsoft Graph **`Calendars.Read`** permission. Daymark does not request `Calendars.ReadWrite`. Complete any administrator approval your organization requires.
4. Copy the **Application (client) ID** into Daymark's Microsoft field under **Account setup**, then save.
5. Choose **Connect Microsoft**, complete browser sign-in/consent yourself, select calendars, and choose **Refresh selected**.

Daymark uses MSAL's public-client flow with the system browser and a protected token cache. See [MSAL browser requirements](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers). The initial adapter targets the public Microsoft cloud, not national-cloud endpoints.

## Refresh, offline use and disconnect

- Sign-in and downloads run away from the WPF UI thread. Refresh shows the current calendar and how many are selected. Cancel during sign-in or download, before saving starts. Sign-in and download have a five-minute timeout. Refresh starts when you request it.
- The account manager shows each calendar's last successful download time in your local time, the downloaded date range in UTC and its cached event count. An empty successful download shows zero events. Calendars without a download have a separate status.
- A failed refresh shows which account needs attention and the retry time if the provider requests a wait. Daymark keeps the last complete download and preserves your checkbox changes while this window is open so you can retry.
- A refresh downloads complete provider-expanded instances for every selected calendar. Calendar pages and account selection commit together only after the whole refresh succeeds. Errors keep the previous cache. The first implementation deliberately uses full bounded snapshots instead of pretending they are incremental sync checkpoints.
- Google and Microsoft recurring instances, moved instances and all-day events are displayed from provider responses. Raw provider payloads remain in the cache; they are not converted into editable local recurring series. Unknown timezone data that cannot be represented safely fails the refresh. Timed events with unknown source zones can display by their authoritative UTC instant.
- **Disconnect** clears saved credentials and downloaded events for that connection from this PC. It does not delete server events or revoke the application's grant on the provider website. Revoke access there if desired. If consent expires, disconnect/reconnect; the current preview does not silently open sign-in while refreshing.
- Local JSON backup exports local calendars only. Provider caches and credentials are excluded; reconnect and refresh to restore connected calendars on another PC. Keep your local event backups.
- Limits: 50 selected calendars per connection, 50,000 instances per calendar, 64 MiB per query/account download and 1,000 pages. Large-account performance and broader provider fixtures remain further work.

## Validation status

Transport/mapping/cache behavior is tested with synthetic provider responses, including pagination, rejected continuation hosts, cancellation and failed transactions. Actual user sign-in and live calendar behavior still require these registrations and end-to-end checks with disposable provider calendars. Two-way editing requires a durable outbox, conditional writes, conflict UI and recovery tests before enabling broader permissions.

Unsigned assemblies can be blocked by Windows Smart App Control. Use signed builds when available; do not disable Windows protections to run this preview. Signing remains pending.
