# UI and preview validation — 7 October 2026

The implementation builds on upstream commit `424b178` (selected file tools,
calibration exports, repository settings and release downloads). Those flows
were retained and styled with the new appearance resources.

Checks completed in the Linux development environment:

- The complete application compiled as C# 5 against Microsoft's .NET Framework
  4.8 reference assemblies, including WPF and the shared release client.
- All 75 runnable generated-data regressions passed. Five existing tests require
  Windows and were skipped. New coverage checks FITS colour planes, CFA offsets,
  gzip/IMAGE HDUs, display stretch, invalid samples, cancellation, XISF endian and
  storage layouts, CFA metadata, zlib/LZ4/byte shuffle, corruption and size limits.
- XAML XML, named control references and resource references passed static checks.
  All 612 XAML property accesses compiled against the Windows reference types.
- `git diff --check` passed.

The Windows smoke command, run by the existing release build, now renders Light,
Dark and hidden-preview layouts and checks generated PNG, JPEG and high-depth
TIFF previews. It also retains search, selection/context-menu, export-option and
repository-header checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Application_Source\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Application_Source\build.ps1
.\Application_Source\dist\AstroArchive.exe --ui-test .\ui-preview
```

WPF rendering and Windows image codecs could not be executed on Linux. Real
camera RAW codecs, cloud folders and real astrophotography captures still need
Windows verification. No Windows smoke screenshots are claimed by this report.

Archive imports remain FITS-only. Other supported formats can be opened for
preview. Large previews are sampled; XISF supports up to 256 MB of decoded samples
and uncompressed/zlib/LZ4 data. zstd, tile-compressed FITS and scientific sequence
cubes need conversion. RAW compatibility follows the installed Windows codecs.
