param([string]$OutputDirectory = (Join-Path $env:TEMP ('AstroArchive-Remote-Tests-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'lib\SMBLibrary.dll') $OutputDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/out:$OutputDirectory\CoreTests.exe" "$PSScriptRoot\DownloadCore.cs" "$PSScriptRoot\SmbSource.cs" "/r:$PSScriptRoot\lib\SMBLibrary.dll" "$PSScriptRoot\CoreTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$OutputDirectory\CoreTests.exe" (Join-Path $OutputDirectory 'fixtures')
if ($LASTEXITCODE -ne 0) { throw 'Remote download checks failed.' }
& $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/out:$OutputDirectory\DiscoveryTests.exe" "$PSScriptRoot\Discovery.cs" "$PSScriptRoot\DiscoveryTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Discovery test compilation failed.' }
& "$OutputDirectory\DiscoveryTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Telescope discovery checks failed.' }
& $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/r:$PSScriptRoot\lib\SMBLibrary.dll" "/out:$OutputDirectory\SmbTests.exe" "$PSScriptRoot\DownloadCore.cs" "$PSScriptRoot\SmbSource.cs" "$PSScriptRoot\SmbTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Direct SMB test compilation failed.' }
& "$OutputDirectory\SmbTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Direct SMB configuration checks failed.' }
