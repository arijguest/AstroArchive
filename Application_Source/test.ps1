# Uses generated FITS/XISF/SER and Windows raster fixtures. .NET Framework 4.8, Windows x64.
param([string]$TestDirectory = (Join-Path $PSScriptRoot 'test-data'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('Model.cs','MountLabels.cs','MountLabelTests.cs','ObservingCities.cs','ObservingCityTests.cs','ColumnLayout.cs','CatalogNames.cs','TableCatalogTests.cs','CaptureGroups.cs','ImportWorkflow.cs','WorkflowTests.cs','ObservationTargets.cs','Fits.cs','Compatibility.cs','FitsAssets.cs','XisfReader.cs','SerReader.cs','RasterHeaders.cs','MetadataProfiles.cs','MetadataReview.cs','CalibrationMatching.cs','AssociatedMetadata.cs','CompatibilityTests.cs','PreviewData.cs','Preview.cs','Help.cs','Xisf.cs','Classification.cs','MosaicMetadata.cs','Mosaics.cs','MosaicExport.cs','InstrumentDetection.cs','CameraDetection.cs','TelescopeConnections.cs','AutoUpload.cs','TelescopeRename.cs','FileState.cs','Pipeline.cs','FileTransfer.cs','ImportEngine.cs','ArchiveReset.cs','FileDeletion.cs','Filters.cs','CaptureScreening.cs','DeletionHistory.cs','SourceCleanup.cs','Repository.cs','ScanSupport.cs','Rotation.cs','PlateSolve.cs','Export.cs','DumpInbox.cs','SirilHandoff.cs','AstroWizardHandoff.cs','PreviewTests.cs','HelpTests.cs','PreviewGestureTests.cs','PerformanceTests.cs','TelescopeTests.cs','MosaicTests.cs','FailedFilenameTests.cs','Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:exe /platform:x64 "/out:$PSScriptRoot\MockAstap.exe" "$PSScriptRoot\MockAstap.cs.txt"
if ($LASTEXITCODE -ne 0) { throw 'Protocol-double compilation failed.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$PSScriptRoot\AstroArchiveTests.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$PSScriptRoot\catalog.csv,catalog.csv" "/resource:$PSScriptRoot\cities.tsv.gz,cities.tsv.gz" "/resource:$PSScriptRoot\Quick_Start.txt,Quick_Start.txt" /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Xml.Linq.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$runDirectory = Join-Path $TestDirectory ('run-' + [Guid]::NewGuid().ToString('N').Substring(0,12))
$results = & "$PSScriptRoot\AstroArchiveTests.exe" $runDirectory
$testExit = $LASTEXITCODE
$results | Write-Output
if ($testExit -ne 0) { throw 'Tests failed.' }

# Separate WIC integration suite, using the same engine with real Windows decoders.
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$rasterSources = $sources | Where-Object { [IO.Path]::GetFileName($_) -notlike '*Tests.cs' }
$rasterSources += @((Join-Path $PSScriptRoot 'RasterReader.cs'), (Join-Path $PSScriptRoot 'RasterTests.cs'))
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$PSScriptRoot\RasterTests.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$PSScriptRoot\catalog.csv,catalog.csv" "/resource:$PSScriptRoot\cities.tsv.gz,cities.tsv.gz" /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Xml.Linq.dll /r:System.Xaml.dll "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" @rasterSources
if ($LASTEXITCODE -ne 0) { throw 'Raster test compilation failed.' }
& "$PSScriptRoot\RasterTests.exe" (Join-Path $runDirectory 'raster')
if ($LASTEXITCODE -ne 0) { throw 'Raster tests failed.' }
