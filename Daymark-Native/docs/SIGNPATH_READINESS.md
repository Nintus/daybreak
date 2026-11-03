# SignPath readiness

This is preparation for the owner's review, not an application or acceptance notice. Check [SignPath's current conditions](https://signpath.org/terms) and [application page](https://signpath.org/apply) before submission.

## Prepared in source

- [x] Repository-level MIT license explicitly covers Daymark's own native and prototype code; third-party notices remain separate.
- [x] Root and native download guides link the code signing and privacy policies.
- [x] Source maintainer/reviewer/release approver is identified in the signing policy.
- [x] Product and version properties are shared by our native assemblies.
- [x] Windows CI checks built metadata, records the source commit, packages documentation and verifies artifact checksums.
- [x] The existing native preview has a public download and source archive.

## Owner and provider steps still required

- [ ] Review and merge the prepared source changes into the intended public development/release branch. Keep the release description accurate for the artifact actually published.
- [ ] Confirm repository ownership, the named roles and MFA on relevant accounts. Configure provider account access yourself; do not put credentials in calendar files, source or exports.
- [ ] Review dependency licenses and the privacy/signing policies against the build being submitted.
- [ ] Submit an accurate application and await SignPath's decision. The project's public history/adoption is limited; there is no acceptance guarantee or promised date.
- [ ] If accepted, configure verified build provenance, metadata restrictions, signing permissions and human approval with the provider. Add the attribution described in the signing policy only when applicable.
- [ ] Sign a reviewed build, verify all required signatures and test that exact downloaded artifact on Windows before marking the launch block resolved.

No SignPath account, certificate, access token, integration or signing request is created by these source changes. The unsigned CI workflow deliberately contains no placeholder credential or pretend signing step.
