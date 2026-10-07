# Uses generated FITS data only. .NET Framework 4.8, Windows x64.
param([string]$TestDirectory = (Join-Path $PSScriptRoot 'test-data'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('Model.cs','ObservationTargets.cs','Fits.cs','PreviewData.cs','Preview.cs','Xisf.cs','Classification.cs','MosaicMetadata.cs','Mosaics.cs','MosaicExport.cs','InstrumentDetection.cs','CameraDetection.cs','TelescopeConnections.cs','AutoUpload.cs','TelescopeRename.cs','FileState.cs','Pipeline.cs','FileTransfer.cs','ImportEngine.cs','ArchiveReset.cs','FileDeletion.cs','Filters.cs','CaptureScreening.cs','DeletionHistory.cs','SourceCleanup.cs','Repository.cs','ScanSupport.cs','Rotation.cs','PlateSolve.cs','Export.cs','DumpInbox.cs','SirilHandoff.cs','AstroWizardHandoff.cs','PreviewTests.cs','PreviewGestureTests.cs','PerformanceTests.cs','TelescopeTests.cs','MosaicTests.cs','Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:exe /platform:x64 "/out:$PSScriptRoot\MockAstap.exe" "$PSScriptRoot\MockAstap.cs.txt"
if ($LASTEXITCODE -ne 0) { throw 'Protocol-double compilation failed.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$PSScriptRoot\AstroArchiveTests.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$PSScriptRoot\catalog.csv,catalog.csv" /r:System.Web.Extensions.dll /r:System.Security.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$runDirectory = Join-Path $TestDirectory ('run-' + [Guid]::NewGuid().ToString('N').Substring(0,12))
$results = & "$PSScriptRoot\AstroArchiveTests.exe" $runDirectory
$testExit = $LASTEXITCODE
$results | Write-Output
if ($testExit -ne 0) { throw 'Tests failed.' }
