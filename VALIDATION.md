# ChargeTrail v2.0.2 validation

Validated on Windows on 4 November 2026:

- Framework C# compiler produced the Windows x64 executable without warnings.
- 30 core checks passed: previous counter/archive checks plus resetting saved and unsaved timers, pausing at zero, preserving notes/settings/archives, deleting only manual trackers, and persisting deletion without recreating the example tracker.
- Management integration checks exercised reset/delete with both confirmation and cancellation, plus cancelled exit preserving the running timer. Background-only mode hid the window and tray icon, counted time, and restored both when reopened.
- Integration checks passed in an isolated temporary data folder: live counting, close-to-tray, background accumulation, periodic durable saves, restart state and backup recovery.
- 12 repeated tab transitions preserved the same shell, sidebar buttons and window handle, with exactly one visible content page. Reselecting the current tab did not rebuild it.
- TR/EN pages and the record/device/manual-tracker dialogs were rendered and visually inspected. Navigation rendering was checked after removing full-shell reconstruction.

The integration suite currently needs at least one local Raw Input device. CI runs the core suite, which supports an empty device list. Physical Bluetooth disconnect/reconnect, actual system sleep/wake and Windows sign-in startup were not repeated in this release. The app does not automatically detect an empty or charged battery.

Run `build.ps1`, then the `--self-test` and `--integration-test` modes described in the README. Test reports, local data, build outputs and diagnostic screenshots are excluded from version control.
