# Third-party software

Daymark's original source is MIT licensed. It includes an unmodified copy of
**ICAL.js 2.2.1** by Philipp Kewisch and contributors in `src/vendor/ical.js`.
ICAL.js is licensed under the Mozilla Public License 2.0. Its full license is
included at `src/vendor/ICAL-LICENSE.txt`; its corresponding source is the
unminified vendored file and [the upstream 2.2.1 release](https://github.com/kewisch/ical.js/tree/v2.2.1).
Daymark's calendar integration lives in separate files under the MIT license.

Windows bundles include Electron 44.5.1 and Chromium. Their notices are supplied
in the runtime's `LICENSE` and `LICENSES.chromium.html` files. Electron's
`resources/default_app.asar` is retained from its distribution and is not used
as Daymark's application source.
