# Build and test

Use Windows x64 with .NET Framework 4.8 and Windows PowerShell 5.1.
The scripts use Windows' C# 5 compiler; Visual Studio, SDK and NuGet are not required.

## Application and isolated tests

From the repository root:

```powershell
.\Application_Source\test.ps1
.\Application_Source\build.ps1
.\Installer\test.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\ui-checks
```

The application tests use generated fixtures. The UI command exercises rendering,
menus, help, tables, previews and progress, and saves screenshots to the chosen folder.
Windows is required for WPF, Windows codecs and native installation checks.

## Complete installer build

Run on a disposable Windows test account: the installation smoke checks register
AstroArchive and create/remove shortcuts.

```powershell
.\scripts\build-release.ps1
```

Output is written to `release-artifacts/`. GitHub Actions runs this validation for
pull requests; results and artifacts are available from the Windows workflow.
See [releasing](RELEASING.md) for versioning, signing and publication.

## Manual checks

- Install, repair, update and uninstall; confirm repositories/settings survive.
- Check both themes, text scaling and keyboard navigation.
- Expand session summaries, use Show all files, and browse Edited projects/targets.
- Preview real images, GIFs, SER and videos with the intended Windows codecs.
- Check USB reconnection and cloud imports with the actual device/provider.
- Use real solver/catalogue installations when checking sky identification.

## Bundled data

`python scripts/build-city-catalog.py DIRECTORY` regenerates the place catalogue
from GeoNames `cities500.txt`, `countryInfo.txt` and `admin1CodesASCII.txt`.
Keep catalogue attribution with distributed data; see the notices in `Application_Source`.
