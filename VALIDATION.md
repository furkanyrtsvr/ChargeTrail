# ChargeTrail v2.0.1 validation

Validated on Windows on 4 November 2026:

- Framework C# compiler produced the Windows x64 executable without warnings.
- 23 core checks passed: time windows, pause/resume, missing devices, locked sessions, long durations, persistence, CSV escaping, immutable archived results, explicit fresh charges, editable notes, language/pinned preferences and legacy JSON compatibility.
- Integration checks passed in an isolated temporary data folder: live counting, close-to-tray, background accumulation, periodic durable saves, restart state and backup recovery.
- 12 repeated tab transitions preserved the same shell, sidebar buttons and window handle, with exactly one visible content page. Reselecting the current tab did not rebuild it.
- TR/EN pages and the record/device/manual-tracker dialogs were rendered and visually inspected. Navigation rendering was checked after removing full-shell reconstruction.

The integration suite currently needs at least one local Raw Input device. CI runs the core suite, which supports an empty device list. Physical Bluetooth disconnect/reconnect, actual system sleep/wake and Windows sign-in startup were not repeated in this release. The app does not automatically detect an empty or charged battery.

Run `build.ps1`, then the `--self-test` and `--integration-test` modes described in the README. Test reports, local data, build outputs and diagnostic screenshots are excluded from version control.
