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
installation is required.

Use [focused checks](../docs/DEVELOPMENT.md) for the changed feature.
The full application suite is `test.ps1`; [release validation](../docs/RELEASING.md)
also covers the installer and updater.

## Notices

[Application licence](../LICENSE) · [licensing scope](../LICENSING.md) ·
[OpenNGC](Catalogue_Notice.md) · [GeoNames](City_Catalogue_Notice.md) ·
[constellation figures](Sky_Catalogue_Notice.md)
