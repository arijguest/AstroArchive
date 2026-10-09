SMBLibrary 1.5.8, Copyright (C) 2014-2026 Tal Aloni and contributors.
Licensed under LGPL-3.0-or-later. This unmodified .NET 4.0 DLL comes from
https://www.nuget.org/packages/SMBLibrary/1.5.8 (lib/net40/SMBLibrary.dll).

The matching upstream source archive is included as SMBLibrary-1.5.8-source.zip
(tag v1.5.8, commit 2edbcf3161084b51dbe11b8a960b0bc7c224b851). Utilities sources,
project files and the upstream build configuration are included in that archive.
The source builds with the .NET SDK and, for the upstream release merge target,
ILRepack. See SMBLibrary.csproj and Utilities.csproj. Only the .NET 4.0 target is
needed by this application. The package does not need System.Buffers for that target.

The application dynamically links the separate, unmodified SMBLibrary.dll. You may
replace it with a compatible build and modify/rebuild the included source under
its license. Reverse engineering for debugging modifications to this library is
permitted. AstroArchive source is available at https://github.com/arijguest/AstroArchive
and Application_Source/build.ps1 builds this combined application from that source.
The third-party license notices do not replace AstroArchive's own license.

DLL SHA-256: 6173fe2fbb3553e23883734eb79dfda12b596db93bd9a56373183f9973d61d51
