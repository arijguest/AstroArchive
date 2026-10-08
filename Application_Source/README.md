# AstroArchive application

Windows x64 application using C# 5, WPF and .NET Framework 4.8.

For installation and use, see the [project README](../README.md) and
[offline user guide](Quick_Start.txt).

## Build and test

Run from the repository root in Windows PowerShell:

```powershell
.\Application_Source\test.ps1
.\Application_Source\build.ps1
```

The application is written to `Application_Source/dist/AstroArchive.exe`.
No Visual Studio, SDK or NuGet installation is required.

For UI checks and complete installer builds, see [development](../docs/DEVELOPMENT.md).
For versioning and publication, see [releasing](../docs/RELEASING.md).

## Bundled data

- [Object catalogue attribution](Catalogue_Notice.md)
- [OpenNGC source acknowledgements](OpenNGC_README.md)
- [Place catalogue attribution](City_Catalogue_Notice.md)
- [Constellation catalogue attribution](Sky_Catalogue_Notice.md)

Application licensing: [PolyForm Noncommercial 1.0.0](../LICENSE); see [scope and notices](../LICENSING.md).
