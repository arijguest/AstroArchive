# AstroArchive: agent guide

Windows 10/11 x64 WPF app in **C# 5 / .NET Framework 4.8**, built with the Windows C# compiler from PowerShell (no Visual Studio, SDK or NuGet). Organises astrophotography: import (folder, USB, watch folders, Seestar/DWARF/Celestron Origin over SMB/FTP), browse, Edited working copies, Analytics, target info, image-quality scoring, handoff to stacking software. Source-available, PolyForm Noncommercial. Website lives in a separate repo (`astroarchive-website`).

## Commands
```powershell
.\Application_Source\build.ps1                       # -> Application_Source\dist\AstroArchive.exe
.\Application_Source\test.ps1 -TargetsOnly           # engine checks, pick by change (table below)
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\<dir> --targets-only --no-updates
.\scripts\build-release.ps1                          # release validation: DISPOSABLE Windows account only
```

## Choose the smallest check for the change
| Change | Engine | UI switch |
| --- | --- | --- |
| Targets, search, selection, Edited export, guide | `test.ps1 -TargetsOnly` | `--targets-only` |
| Target info, thumbnails, sky location | `-TargetInfoOnly` | `--info-only` |
| Planetary sky, capture clocks, AVI/SER | `-SkyOnly` | `--sky-only` |
| Export window, stacking folders, handoffs, AstroBin CSV | `-ExportsOnly` | full UI suite |
| Preview resolution/layout | `-PreviewOnly` | `--preview-only` |
| Analytics | `-AnalyticsOnly` | `--analytics-only` |
| Checkbox/focus/theme states | build | `--controls-only` |
| Startup splash / window restore | `test-startup.ps1` | `--startup-only` |
| Network discovery / transfers / live import | `-RemoteOnly` + `test-remote-transports.ps1` | `--remote-only` |
| Background processes, archive claims | `AstroArchiveTests.exe <dir> --background-only` | full UI (Activity) |
| Broad engine change / release prep | `test.ps1` | full UI suite |

Copy, docs or cosmetic changes: build + link/content check + visual review only; no full suites. UI runs use isolated settings and a separate output folder. Details: `docs/DEVELOPMENT.md`.

## Layout (do not list or read `Application_Source/` wholesale: ~400 flat files)
Per feature `X`: `X.cs` engine · `XUi.cs` WPF UI · `XTests.cs` engine checks · `XSmoke.cs` UI checks. Find the feature by prefix with Grep/Glob (`Import*`, `Remote*`, `Target*`, `Sky*`, `Analytics*`, `Edited*`, `Quality*`, `Preview*`, `Metadata*`, `Export*`, `Telescope*`, `Information*`, `BackgroundWork*`, `Localization*`). Start at `X.cs`, read `XTests.cs` for expected behaviour, touch `XUi.cs` only for UI work. Other dirs: `Installer/` (launcher, setup, `release.json`, `Payload/`), `scripts/` (data builders, release verifiers), `docs/`, `.github/workflows/`, `releases/offline-1.2.0` (legacy bridge; leave alone).

## Hard rules
- **C# 5 only**: no string interpolation (`$""`), `?.`, `??=`, `nameof`, expression-bodied members, auto-property initialisers, `out var`, tuples, pattern matching.
- Ordinary imports/exports **never alter image bytes**; source originals are kept by default; preview stretches affect display only; "Fill missing metadata" never replaces existing values or user edits; scores suggest, never reject.
- Bulk delete/move/reset/rename/protect operations must take exclusive claims (`BackgroundWork.cs`); long read-mostly work runs in the background lane and reports to Activity, never a modal. Results must be idempotent.
- One running instance per repository (archive); keep `.astroarchive` and `Edited` together.
- Preserve attribution/licence notices for bundled data and code: OpenNGC, GeoNames, D3-Celestial, SMBLibrary (separate replaceable DLL with matching source).
- Seven languages (en, de, es, fr, it, ja, zh-Hans): new user-facing strings need entries per `Application_Source/Localization/README.md` (`lang-<code>.txt` files); check layouts for long German/French text.
- Generated files are ignored (`dist/`, `test-data/`, `Installer/Payload/AstroArchive.exe` etc.). Never commit them, user images, FITS files or secrets.

## Releases (don't do these unless asked)
Version = `application_version` + `installer_revision` in `Installer/release.json`; keep `AssemblyInfo`, app manifest, bundled guide and `Installer/Payload/Release_Notes.txt` aligned (`docs/RELEASING.md`). **A push to `main` can publish a release**; published releases are immutable. Never push version tags; prereleases run manually from a branch. Signing is optional for 3.2.7+ (`require_signing: false`). After a release, the website repo needs its version/download labels updated.

## Git
Work on a branch and open a PR; never push to `main` unless asked. Commit messages: short imperative subject. Don't commit unless asked.

## Brain2: the owner's second brain (read first, saves tokens)
Local Obsidian vault `C:\Users\arija\OneDrive\Obsidian\Brain2` (private repo `arijguest/Brain2`). If reachable (Claude Code `--add-dir`, Codex `--add-dir`), read the specific note before searching this repo:
- `01 Projects/AstroArchive.md`: status, feature map, conventions, open questions
- `03 Resources/AstroArchive/AstroArchive Code Map.md`: feature -> file prefixes (use instead of exploring)
- `03 Resources/AstroArchive/AstroArchive Testing Matrix.md`: which check to run
- `03 Resources/AstroArchive/AstroArchive Release Process.md`: versioning/signing
- `03 Resources/AI Workflow/Model Selection.md`, `Token Optimisation.md`
Read only the note you need, never the whole vault; `Repos/` in the vault is a generated mirror of this repo, ignore it. After a substantial task, append one dated line (what changed, gotchas, next step) to the hub note's **Log**; edit the vault only, don't commit it unless asked. If the vault isn't reachable, this file is sufficient.

## Token discipline
- Grep/Glob first, then read ranges (offset/limit). Never read `dist/`, `test-data/`, `*.gz`, `*.zip`, `*.tsv.gz`, `catalog.csv`, `TargetInfo.zip`, images, `docs/images`, or lockfiles.
- Name the files/tests you need; run the smallest check; pipe build/test output through tail/filter and report real failures verbatim.
- Don't re-read files you just edited, don't paste large diffs back, prefer targeted edits over rewrites, use `git diff --stat` before full diffs.
- One task per session; `/clear` (Claude) or a fresh thread (Codex) between tasks; compact with a focus hint on long ones. Batch independent tool calls. Be concise: lead with the result, no recap.
- Models: default Claude **Sonnet 5.5 Medium** / Codex **Sol 6.1 Low**. Escalate to **Opus 5.5 Medium** / **Sol 6.1 Medium** only for: import/transfer integrity, backup/restore, installer/update/rollback, background-lane claims, signing, root cause unclear after two failed attempts, or changes spanning 4+ files with shared invariants. Plan on the higher tier, implement on the lower.
