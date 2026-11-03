# Daymark

An open-source calendar, built Windows first with C# and WPF. The portable calendar core is separate from the Windows interface. The goal is a dependable primary calendar with a native feel.

The native preview supports local timed and all-day events, month/week/day views, an agenda, search, JSON backups, and session undo/redo. Account sync, recurrence, reminders and timezone-aware events are planned. See the [native client guide](Daymark-Native/README.md) and [roadmap](Daymark-Native/ROADMAP.md).

## Downloads

The [releases page](https://github.com/Nintus/daybreak/releases) describes the capabilities of each published preview. Development-branch features may be newer than the latest release. Current Windows builds are **unsigned** and can be blocked by Smart App Control.

## Code signing policy

Read the [code signing policy](Daymark-Native/docs/CODE_SIGNING.md), [privacy policy](Daymark-Native/docs/PRIVACY.md), and [SignPath readiness checklist](Daymark-Native/docs/SIGNPATH_READINESS.md). SignPath acceptance and signing integration are pending; no certificate or sponsorship is claimed.

## Development and license

Use the .NET 10 SDK for the Windows client. [Build instructions](Daymark-Native/README.md#build-on-windows) include the core checks. `Daymark-Source/` contains the earlier web/Electron prototype and is reference material for the Windows-first direction.

Daymark's own source, including the native client, is licensed under the [MIT License](LICENSE). Third-party components retain their own licenses and notices. Packaged builds include the applicable runtime notices.
