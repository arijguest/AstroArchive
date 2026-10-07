# Windows 10/11 x64, C# 5 and the built-in .NET Framework 4.8 compiler.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Tests.cs' } | ForEach-Object { $_.FullName }
$sources += @((Join-Path $PSScriptRoot '..\Installer\InstallCore.cs'), (Join-Path $PSScriptRoot '..\Installer\Updates.cs'))
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', "/out:$OutputDirectory\AstroArchive.exe", "/win32manifest:$PSScriptRoot\app.manifest", "/resource:$PSScriptRoot\MainWindow.xaml,MainWindow.xaml", "/resource:$PSScriptRoot\catalog.csv,catalog.csv", "/resource:$PSScriptRoot\Assets\AstroArchive_Logo.png,AstroArchive_Logo.png", "/win32icon:$PSScriptRoot\Assets\AstroArchive.ico", '/r:System.Web.Extensions.dll', '/r:System.Xml.dll', '/r:System.Security.dll', "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll", '/r:System.Xaml.dll') + $sources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output (Join-Path $OutputDirectory 'AstroArchive.exe')
