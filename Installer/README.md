# Windows installer

Requires Windows 10 or 11 x64 and .NET Framework 4.8.

## Install, update or repair

Run the downloaded AstroArchive installer. New installations default to
`%LOCALAPPDATA%\Programs\AstroArchive`; updates use the registered installation.
The default location requires no administrator access. Choose a local application folder.

Setup creates desktop and Start menu shortcuts and an entry in Windows Installed Apps.
Running the same package repairs application files; older packages cannot replace newer ones.
Uninstall through Installed Apps. Repositories, images and user settings are retained.

See [updates and troubleshooting](../docs/UPDATES.md) for automatic updates,
offline launch, checksums and Windows policy blocks.

## Command line

Replace the filename with your downloaded package:

```powershell
.\AstroArchive2.1.9.1.exe --silent
.\AstroArchive2.1.9.1.exe --update --silent --root "$env:LOCALAPPDATA\Programs\AstroArchive"
.\AstroArchive2.1.9.1.exe --uninstall --root "$env:LOCALAPPDATA\Programs\AstroArchive"
```

`--update` requires an existing installation. `--restart` opens the app after a
successful silent installation. Exit code 0 means success; 1 means failure or incomplete setup.
Errors are written to `%TEMP%\AstroArchive-setup-error.txt`.

## Build

From the repository root, run:

```powershell
.\Application_Source\build.ps1
.\Installer\test.ps1
.\Installer\build.ps1
```

The installer uses `Application_Source/dist/AstroArchive.exe` and the version in
`Installer/release.json`. Output is written to `Installer/dist`.
See [development](../docs/DEVELOPMENT.md) for a complete build and
[releasing](../docs/RELEASING.md) for package versions and signing.
