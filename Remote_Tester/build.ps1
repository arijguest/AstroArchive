# Build the portable tester on Windows without Visual Studio or NuGet.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$app = Join-Path $PSScriptRoot '..\Application_Source'
$compilerArguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:5',"/out:$OutputDirectory\AstroArchive.RemoteTester.exe", "/win32icon:$app\Assets\AstroArchive.ico", "/win32manifest:$PSScriptRoot\remote.manifest", "/resource:$PSScriptRoot\RemoteWindow.xaml,RemoteWindow.xaml", "/resource:$app\Assets\AstroArchive_Logo.png,AstroArchive_Logo.png", "/r:$PSScriptRoot\lib\SMBLibrary.dll", '/r:System.Web.Extensions.dll','/r:System.Windows.Forms.dll','/r:System.Xaml.dll', "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll", "$PSScriptRoot\Program.cs", "$PSScriptRoot\DownloadCore.cs", "$PSScriptRoot\Discovery.cs", "$PSScriptRoot\SmbSource.cs", "$PSScriptRoot\StartupDiagnostics.cs", "$app\Theme.cs")
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Remote tester compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'AstroArchive.RemoteTester.exe.config') $OutputDirectory
Copy-Item (Join-Path $PSScriptRoot 'README.md') $OutputDirectory
Copy-Item (Join-Path $PSScriptRoot 'lib\SMBLibrary.dll') $OutputDirectory
Copy-Item (Join-Path $PSScriptRoot 'lib') (Join-Path $OutputDirectory 'ThirdParty') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'Collect-diagnostics.ps1') $OutputDirectory
Copy-Item (Join-Path $PSScriptRoot '..\LICENSE') $OutputDirectory
Write-Output (Join-Path $OutputDirectory 'AstroArchive.RemoteTester.exe')
