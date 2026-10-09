param([string]$OutputDirectory = (Join-Path $env:TEMP ('AstroArchive-Remote-Tests-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/out:$OutputDirectory\CoreTests.exe" "$PSScriptRoot\DownloadCore.cs" "$PSScriptRoot\CoreTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$OutputDirectory\CoreTests.exe" (Join-Path $OutputDirectory 'fixtures')
if ($LASTEXITCODE -ne 0) { throw 'Remote download checks failed.' }
