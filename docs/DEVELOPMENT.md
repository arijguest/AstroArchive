# Build and check

Use Windows x64, .NET Framework 4.8 and PowerShell. The scripts use Windows' C# 5
compiler; Visual Studio, SDK and NuGet are not required.

## Build

From the repository root:

~~~powershell
.\Application_Source\build.ps1
~~~

Output: `Application_Source/dist/AstroArchive.exe`.

## Choose checks by the change

| Change | Engine checks | UI checks |
| --- | --- | --- |
| Targets, search, selection, Edited export, guide | `test.ps1 -TargetsOnly` | `--targets-only` |
| Preview resolution, layout, sub exposure labels | `test.ps1 -PreviewOnly` | `--preview-only` |
| Analytics calculations, layout and export | `test.ps1 -AnalyticsOnly` | `--analytics-only` |
| Checkbox, focus, disabled or theme states | Build the app | `--controls-only` |
| Broader engine changes or release preparation | `test.ps1` | Full UI suite |

Example:

~~~powershell
.\Application_Source\test.ps1 -TargetsOnly
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\target-checks --targets-only --no-updates
~~~

Use the corresponding preview/control switch and a separate output folder for
those checks. UI runs use isolated settings and generated fixtures; they save
results and screenshots. Repeat after relevant changes or failures. Copy and
cosmetic changes need a build, link/content checks and visual review as applicable,
not repeated full suites.

Analytics checks cover integration accounting, missing metadata, date/telescope
scoping, histogram boundaries, continuation rankings and SVG/PDF structure. The
Windows UI switch checks both themes, live scope changes and all four export
formats, including combined sheets, image decoding and safe file replacement:

~~~powershell
.\Application_Source\test.ps1 -AnalyticsOnly
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\analytics-ui --analytics-only --no-updates
~~~

Full checks:

~~~powershell
.\Application_Source\test.ps1
.\Installer\test.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\ui-checks --no-updates
~~~

## Release validation

~~~powershell
.\scripts\build-release.ps1
~~~

Run on a disposable Windows account: installer checks register the app and
create/remove shortcuts. Output goes to `release-artifacts`. GitHub's Windows
workflow validates pull requests. See [versioning and signing](RELEASING.md).

## Manual coverage

Use real files/devices for codec support, USB reconnection, cloud storage and
external processor/solver handoffs. Check both themes, keyboard navigation and
text scaling. Verify install, repair, update and uninstall preserve user data.

The website has separate Sites source. Update its existing project, preserve the
audience and layout, and keep resource links aligned with this repository.

## Bundled data

`scripts/build-city-catalog.py` regenerates the place catalogue from GeoNames
`cities500.txt`, `countryInfo.txt` and `admin1CodesASCII.txt`. Preserve the attribution
notices in Application_Source with distributed data.
