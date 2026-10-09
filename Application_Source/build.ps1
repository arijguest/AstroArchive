# Windows 10/11 x64, C# 5 and the built-in .NET Framework 4.8 compiler.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Tests.cs' } | ForEach-Object { $_.FullName }
$sources += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Remote') -Filter '*.cs' | Where-Object { $_.Name -notlike '*Tests.cs' } | ForEach-Object { $_.FullName }
$sources += @((Join-Path $PSScriptRoot '..\Installer\ReleaseMonitor.cs'), (Join-Path $PSScriptRoot '..\Installer\InstallCore.cs'), (Join-Path $PSScriptRoot '..\Installer\Updates.cs'), (Join-Path $PSScriptRoot '..\Installer\WindowsIntegration.cs'), (Join-Path $PSScriptRoot '..\Installer\ShellIdentity.cs'))
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', "/out:$OutputDirectory\AstroArchive.exe", "/win32manifest:$PSScriptRoot\app.manifest", "/resource:$PSScriptRoot\MainWindow.xaml,MainWindow.xaml", "/resource:$PSScriptRoot\catalog.csv,catalog.csv", "/resource:$PSScriptRoot\cities.tsv.gz,cities.tsv.gz", "/resource:$PSScriptRoot\sky-constellations.txt,sky-constellations.txt", "/resource:$PSScriptRoot\Quick_Start.txt,Quick_Start.txt", "/resource:$PSScriptRoot\..\LICENSE,LICENSE", "/resource:$PSScriptRoot\Assets\AstroArchive_Logo.png,AstroArchive_Logo.png", "/resource:$PSScriptRoot\Assets\AstroArchive.ico,AstroArchive.ico", "/win32icon:$PSScriptRoot\Assets\AstroArchive.ico", "/r:$PSScriptRoot\Remote\lib\SMBLibrary.dll", '/r:System.Web.Extensions.dll', '/r:System.Xml.dll', '/r:System.Security.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:System.Xml.Linq.dll', "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll", '/r:System.Xaml.dll', "/r:$wpf\UIAutomationProvider.dll", "/r:$wpf\UIAutomationTypes.dll") + $sources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'Remote\lib\SMBLibrary.dll') $OutputDirectory -Force
$thirdParty = Join-Path $OutputDirectory 'ThirdParty'
New-Item -ItemType Directory -Force -Path $thirdParty | Out-Null
Get-ChildItem (Join-Path $PSScriptRoot 'Remote\lib') | Where-Object { $_.Name -ne 'SMBLibrary.dll' } | Copy-Item -Destination $thirdParty -Force
Copy-Item (Join-Path $PSScriptRoot '..\LICENSE') (Join-Path $OutputDirectory 'LICENSE.txt') -Force
Copy-Item (Join-Path $PSScriptRoot '..\LICENSING.md') $OutputDirectory -Force
Copy-Item (Join-Path $PSScriptRoot 'Quick_Start.txt') $OutputDirectory -Force
Write-Output (Join-Path $OutputDirectory 'AstroArchive.exe')
