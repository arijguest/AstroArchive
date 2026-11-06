# Import and stacking refinements

This change targets the 3.3.3 source in website-release/app and addresses import-list management, responsive file
operations and safe automatic stacking. It does not publish or update a release.

## Plan and implementation

1. Add selection-aware import context/dropdown actions: edit metadata, remove
   rows from the current plan, and explicitly delete existing archive copies.
   Removing a row never removes a source file. A fresh scan rebuilds the list.
2. Keep browsing controls and menus available during serialized background
   writes. Read the resulting index/gallery off the dispatcher; publish detached
   metadata replacements and refreshed snapshots after work, including cancel.
   Full-image previews use an independent read-only worker.
3. Avoid redundant full-file hashing for metadata moves only when reliable,
   unchanged local file identities and change stamps verify the indexed bytes.
   Cloud, changed and unsupported filesystems retain checksum validation.
4. Add selected/all short-sub stacking under Import and Repository. Use
   the existing native quality engine, accept green/high and yellow/mid only, and offer
   quality weighting by default. Use Siril's local CLI for calibration,
   registration and integration, following the AstroAIO workflow's verified
   working copies, quality metrics and recovery principles.
5. Preserve originals until outputs validate, import, checksum and checkpoint.
   Optional archive replacement additionally verifies recovery copies and a
   durable manifest before cleanup. Keep rejected subs, calibrations and source
   mirrors. Provide verified recovery through the Repository dropdown.
6. Recognize compound Seestar calibration names/master headers. Use compatible
   dark temperatures within 3 C first, otherwise the nearest available set and
   a warning. Reuse selection rules in stacking-folder and processor exports.

## Local verification

Engine checks generate disposable FITS fixtures under `Application_Source/test-data`.
`test.ps1 -StackingOnly` runs stacking safety, calibration and metadata batch
checks; the normal `test.ps1` includes these and the existing regression suite.
When Siril 1.4 or later is installed, a real CLI fixture also registers and integrates an
artificial star field. External-runner doubles exercise failure and cancellation.

Native checks use isolated settings and repositories:

```powershell
.\Application_Source\build.ps1
.\Application_Source\test.ps1 -SkipInstalledSiril
.\Application_Source\test.ps1 -StackingOnly
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\background-local --background-only --no-updates
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\imports-local --import-management-only --no-updates
```

The background check holds a real metadata worker, opens menus, searches,
changes pages/filters, opens a full preview and checks conflicting writes stay
serialized. It also cancels a real deletion batch and verifies remaining bytes,
deletion history and the refreshed library. Import checks cover explicit
selection and action scopes. Production telescope files, cloud round trips and
USB removal remain manual integration coverage.

## Validation on 29 October 2026

The local 3.3.3 build succeeds. Existing distribution/version edits were retained;
this work did not build a release installer or publish anything.

| Check | Result |
| --- | --- |
| Broad engine regressions, `test.ps1 -SkipInstalledSiril` | Exit 0; harness reports 680 passed, 6 optional checks skipped |
| Windows image codec integration | All 6 checks passed |
| Native background operations | Passed real FITS metadata moves, canceled deletion, checksum/audit preservation, independent preview and continued browsing |
| Native import management | Passed explicit selection, right-click batch preservation, metadata/removal actions and selected/all stacking scopes |
| Installed Siril 1.4.4 | Real weighted integration and full raw calibration recipe passed; latest code also passed process completion/cancellation and exact recovery checks |
| Full native UI suite | Reaches an existing south-cardinal-label sky-preview layout assertion; reproduced separately on untouched 3.3.3 HEAD at this display scaling |

Four installed-Siril cases were skipped in the broad repeat because their real
integration runs were validated separately; two optional decoder backends were
unavailable. The installed-engine evidence distinguishes actual scientific
processing from replaying its verified FITS outputs through the latest cleanup
and recovery code.

Evidence is under `Application_Source/test-data/`: `refinements-engine-final.log`,
`final-v333-background/`, `final-v333-import-management/`, and
`stack-verification/verification.txt`. The unchanged baseline sky failure is in
`baseline-sky-visible-333/ui-smoke-error.txt`; the full current UI result is in
`ui-refinements-v333/`. No production sky layout was changed for this task.
