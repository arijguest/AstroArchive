# AstroArchive application

Windows x64 app using C# 5, WPF and .NET Framework 4.8.

See the [project overview](../README.md) and [offline user guide](Quick_Start.txt)
for installation and use.

## Build

From the repository root:

~~~powershell
.\Application_Source\build.ps1
~~~

Output: `Application_Source/dist/AstroArchive.exe`. No Visual Studio, SDK or NuGet
installation is required. Distribute the whole `dist` folder: the application
needs `SMBLibrary.dll` beside the executable. Keep `ThirdParty` and the bundled
licence/notice files with the application.

Use [focused checks](../docs/DEVELOPMENT.md) for the changed feature.
The full application suite is `test.ps1`; [release validation](../docs/RELEASING.md)
also covers the installer and updater.

Released versions include automatic telescope discovery, selected network imports,
live imports and independent sessions for multiple telescopes. See
[network setup and implementation checks](../docs/REMOTE_IMPORT.md)
for the user workflow, firmware requirements and read-only SMB/FTP fixtures.
Python is used only for the optional transport checks, not by the application.
**Repository → Analytics** provides six charts and PNG, JPEG, PDF and SVG exports;
see the [user guide](Quick_Start.txt) for report scoping and export options.

## Notices

[Application licence](../LICENSE) · [licensing scope](../LICENSING.md) ·
[OpenNGC](Catalogue_Notice.md) · [GeoNames](City_Catalogue_Notice.md) ·
[constellation figures](Sky_Catalogue_Notice.md) · [SMBLibrary](Remote/lib/README.md)
