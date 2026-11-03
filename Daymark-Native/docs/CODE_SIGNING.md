# Code signing policy

Status: **preparing for application; signing is not enabled**. Daymark has not been accepted by SignPath Foundation and current downloads are unsigned. A checksum verifies file integrity against the published checksum; it is not a publisher signature.

## Responsibilities

The repository owner [Nintus](https://github.com/Nintus) is the current source maintainer, reviewer and designated release approver. External changes require maintainer review. Automated build results support review but do not authorize a release. Any future signing credentials or provider permissions must be limited to the approved signing workflow. Maintainers must confirm MFA before provider enrollment; this repository does not assert that account settings have been verified.

## Release approval

Before a signed release, the approver records the reviewed source commit, build run, artifact identity and intended version. Our app and core library must share product/version metadata. Third-party runtime files retain their original identity and notices.

Once a provider is configured, signing must use its verified build-origin integration and require human approval for each release. Sign only artifacts from the reviewed repository build, then check signatures and launch that exact artifact on Windows. Publish the source commit, checksums and signature status with the download. Never label a build signed merely because CI succeeded or a signing request was sent.

Existing preview release assets must not be silently replaced. Release publication is a separate, deliberate action; unsigned CI artifacts remain explicitly identified as development builds. Changes to signing configuration require the same review as executable code.

## Provider status and attribution

We are evaluating [SignPath Foundation](https://signpath.org/) under its [published conditions](https://signpath.org/terms). Acceptance is not guaranteed. If accepted and their service is actually used, update the project home and release/download pages with their required attribution: “Free code signing provided by SignPath.io, certificate by SignPath Foundation”. That statement is conditional preparation, not current sponsorship.

See the [privacy policy](PRIVACY.md) and [remaining owner steps](SIGNPATH_READINESS.md).
