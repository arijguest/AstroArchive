# Prerelease 3.2.8 handoff

Branch: `prerelease3.2.8`. Package version: `3.2.8.1`.
Base: latest stable `v3.2.7.2`, commit
`76e63ab1a23c7d6ac000a760483c5ee5684b7959`. All stable application,
installer recovery and optional-signing changes are retained.

## Changes

- Audited all 326 thumbnail assignments; repaired Commons page-template/control
  contamination and normalized whitespace in 88 records. Original image bytes,
  licences and scientific metadata are unchanged. Repairs are also applied by the
  data-pack exporter, with `runtime_credit_revision: 1` recorded in the pack.
- Comet guidance now provides practical starting exposures and a one-pixel drift
  calculation. Cards without guidance omit imaging notes; Windows smoke coverage
  explicitly checks the empty state.
- Chosen capture towns now supply historical timezone rules and update stored UTC
  when the location changes, using the original acquisition clock each time.
  Explicit timestamp zones and declared header standards retain their meaning;
  an explicitly entered timezone wins. Existing records with chosen towns and
  stale assumed UTC are repaired on archive open, including source/restore snapshots.
- Santa Ponsa's `2026-08-12T20:32:54.797` local summer clock converts to
  `2026-08-12T18:32:54.797Z`. This corrects the Sun from roughly -18° to +2°
  altitude and restores eclipse detection. The offline approximation gives 99.8%
  coverage here; no adjustment was made to force the model's classification to Total.
- Windows smoke checks exercise refreshed preview identity and eclipse-cache
  invalidation after a capture-town edit. Metadata help is translated in all six
  bundled non-English languages.

## Validation

Local Linux/Mono verification on 13 October 2026:

- 99 focused target/metadata/information/localization engine checks passed.
- 52 focused sky/eclipse/video checks passed.
- 2 Python credit checks passed, auditing all 326 assignments and image hashes.
- 11 release-report checks passed.
- Full WPF application compiled against .NET Framework 4.8 reference assemblies
  with seven existing warnings. Linux cannot execute WPF rendering.
- Compared the repaired pack against the stable pack: all image bytes, licence
  terms/links, source links, identities, scientific data and notices are unchanged.
- `git diff --check` passed.

Windows validation passed for implementation commit `68839b3`:

- [Target information pane](https://github.com/arijguest/AstroArchive/actions/runs/38337070529): engine, installer, build and rendering checks in all seven languages.
- [Planetary sky positions](https://github.com/arijguest/AstroArchive/actions/runs/38337070069): all 52 engine checks, application build, and marker/metadata-refresh/cache rendering checks.

Publishing is separate:
the release workflow supports an explicit `prerelease` input. This handoff does
not publish a release or change the stable update feed.

More detail: [capture clocks](PLANETARY_SKY.md#capture-town-clocks-in-328) and
[credit audit](TARGET_INFORMATION.md#credit-audit-and-imaging-notes-in-328).
