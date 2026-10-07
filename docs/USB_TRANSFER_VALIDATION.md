# Local USB import and telescope rename validation

The combined 1.6.0 application builds as C# 5 against .NET Framework 4.8. The
Windows release workflow runs all generated-data regressions and WPF smoke checks
before publishing the installer. Build logs record the executed test counts.

Generated-data coverage includes saved profile migration and serialization,
repository recovery, saved overrides, USB volume identity and changed drive paths,
duplicate screening, incremental imports, DWARF sidecars, missing/damaged archive
restoration, changed contents with matching size/time, cancellation/disconnection,
isolated failures, rename collisions/corruption, rollback, stable sessions, and
recovery of interrupted file/index/settings changes. Cross-feature tests also
cover USB deletion exclusions after source renaming, telescope rename updates to
deletion history, and retention of rejected or damaged captures for review.

WPF smoke checks cover profile recovery/selection, retention of the USB setup
source, stale scan invalidation, upload visibility/busy state, combined filters,
filtered import counts, live status changes, previews and popup scrolling.

Run `Application_Source/test.ps1` and `scripts/build-release.ps1` on Windows.
Physical Seestar/DWARF USB arrival/removal and USB devices reported as fixed disks
still require verification with real hardware. This release uses local USB
storage and includes no network discovery or direct Wi-Fi client.
