# Build and check

Use Windows x64, .NET Framework 4.8 and PowerShell. The scripts use Windows' C# 5
compiler; Visual Studio, SDK and NuGet are not required.

## Build

From the repository root:

~~~powershell
.\Application_Source\build.ps1
~~~

Output: `Application_Source/dist/AstroArchive.exe`. Keep the adjacent
`SMBLibrary.dll`, `ThirdParty` source/licences and bundled notices when copying
or packaging this folder. The embedded offline guide comes from
`Application_Source/Quick_Start.txt`; rebuild after changing it.

## Choose checks by the change

| Change | Engine checks | UI checks |
| --- | --- | --- |
| Targets, search, selection, Edited export, guide | `test.ps1 -TargetsOnly` | `--targets-only` |
| Preview resolution, layout, sub exposure labels | `test.ps1 -PreviewOnly` | `--preview-only` |
| Analytics calculations, layout and export | `test.ps1 -AnalyticsOnly` | `--analytics-only` |
| Checkbox, focus, disabled or theme states | Build the app | `--controls-only` |
| Network discovery, selected transfers, live imports | `test.ps1 -RemoteOnly`; transport checks below | `--remote-only` |
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

## Network and live import checks

The remote engine checks use generated files and a simulated live producer,
including changing/incomplete files, cache reuse, metadata freshness, cancellation
and disconnect recovery. The UI checks use generated discovery and capture data.
Neither requires a telescope or a change to Windows SMB policies.

For actual read-only SMB/FTP loopback fixtures, also install Python 3.12 with
`python` on PATH and the pinned test dependencies:

~~~powershell
python -m pip install impacket==0.13.1 pyftpdlib==2.2.0
.\Application_Source\test.ps1 -RemoteOnly
.\Application_Source\test-remote-transports.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\Application_Source\test-data\remote-ui --remote-only --no-updates
~~~

Run `test.ps1` first: it builds the `Application_Source/AstroArchiveTests.exe`
used by the transport script. The fixtures listen only on loopback ports 24445
and 24421; those ports must be free. The script stops its fixture servers when it
finishes. Python and these packages are not application runtime requirements.

The [v3 workflow](../.github/workflows/v3.yml) also checks the existing
`--connected-import-only` and `--controls-only` UI flows and builds a portable ZIP
and installer without publishing a release. See [network import details](REMOTE_IMPORT.md)
and [recorded preview validation](V3_VALIDATION.md).

## Release validation

~~~powershell
.\scripts\build-release.ps1
~~~

Run on a disposable Windows account: installer checks register the app and
create/remove shortcuts. Output goes to `release-artifacts`. GitHub's Windows
workflow validates pull requests. See [versioning and signing](RELEASING.md).

## Manual coverage

Use real files/devices for codec support, USB/network reconnection, cloud storage and
external processor/solver handoffs. Check both themes, keyboard navigation and
text scaling. Verify install, repair, update and uninstall preserve user data.
For network imports, check discovery and file access on each model/firmware,
live capture while downloading, interrupted transfers and duplicate-free retry.
Compare archived bytes with separately copied telescope originals; loopback checks
do not establish hardware compatibility or Wi-Fi throughput.

The website has separate Sites source. Update its existing project, preserve the
audience and layout, and keep resource links aligned with this repository.

## Bundled data

`scripts/build-city-catalog.py` regenerates the place catalogue from GeoNames
`cities500.txt`, `countryInfo.txt` and `admin1CodesASCII.txt`. Preserve the attribution
notices in Application_Source with distributed data.
