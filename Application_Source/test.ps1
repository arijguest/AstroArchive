# Uses generated FITS/XISF/SER and Windows raster fixtures. .NET Framework 4.8, Windows x64.
param([string]$TestDirectory = (Join-Path $PSScriptRoot 'test-data'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('StackingApps.cs','StackingAppsTests.cs','Model.cs','MountLabels.cs','MountLabelTests.cs','ObservingCities.cs','ObservingCityTests.cs','ColumnLayout.cs','CatalogNames.cs','TableCatalogTests.cs','CaptureGroups.cs','SubframeSessions.cs','MediaFiles.cs','CaptureSessions.cs','FilterTests.cs','TargetNavigation.cs','TargetNavigationTests.cs','ImportWorkflow.cs','WorkflowTests.cs','ObservationTargets.cs','Fits.cs','Compatibility.cs','FitsAssets.cs','XisfReader.cs','SerReader.cs','RasterHeaders.cs','MetadataProfiles.cs','MetadataReview.cs','MetadataEditing.cs','MetadataEditingTests.cs','CalibrationMatching.cs','AssociatedMetadata.cs','CompatibilityTests.cs','PreviewData.cs','PreviewCache.cs','Preview.cs','Help.cs','Xisf.cs','Classification.cs','SkyWcs.cs','InstrumentDetection.cs','CameraDetection.cs','TelescopeConnections.cs','AutoUpload.cs','TelescopeRename.cs','FileState.cs','Pipeline.cs','FileTransfer.cs','ImportEngine.cs','ArchiveReset.cs','FileDeletion.cs','Filters.cs','FileSearch.cs','SearchWork.cs','SearchWorkTests.cs','FileSearchTests.cs','ImportPolicy.cs','ImportPolicyTests.cs','CaptureScreening.cs','DeletionHistory.cs','SourceCleanup.cs','Repository.cs','ScanSupport.cs','SourceHistory.cs','SessionScanCache.cs','FilenameScanCache.cs','FastImportTests.cs','SessionScanTests.cs','Rotation.cs','PlateSolve.cs','Export.cs','DumpInbox.cs','EditedWorkspace.cs','EditedMetadata.cs','EditedTargetMatching.cs','EditedImport.cs','EditedDuplicates.cs','EditedTests.cs','SirilHandoff.cs','PreviewTests.cs','HelpTests.cs','PreviewGestureTests.cs','SkyContext.cs','SkyContextTests.cs','PerformanceTests.cs','TelescopeTests.cs','FailedFilenameTests.cs','Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:exe /platform:x64 "/out:$PSScriptRoot\MockAstap.exe" "$PSScriptRoot\MockAstap.cs.txt"
if ($LASTEXITCODE -ne 0) { throw 'Protocol-double compilation failed.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$PSScriptRoot\AstroArchiveTests.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$PSScriptRoot\catalog.csv,catalog.csv" "/resource:$PSScriptRoot\cities.tsv.gz,cities.tsv.gz" "/resource:$PSScriptRoot\sky-constellations.txt,sky-constellations.txt" "/resource:$PSScriptRoot\Quick_Start.txt,Quick_Start.txt" /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Xml.Linq.dll @sources
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
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$PSScriptRoot\RasterTests.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/resource:$PSScriptRoot\catalog.csv,catalog.csv" "/resource:$PSScriptRoot\cities.tsv.gz,cities.tsv.gz" "/resource:$PSScriptRoot\sky-constellations.txt,sky-constellations.txt" /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Xml.Linq.dll /r:System.Xaml.dll "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" @rasterSources
if ($LASTEXITCODE -ne 0) { throw 'Raster test compilation failed.' }
& "$PSScriptRoot\RasterTests.exe" (Join-Path $runDirectory 'raster')
if ($LASTEXITCODE -ne 0) { throw 'Raster tests failed.' }
