param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'test-data\startup'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& $compiler /nologo /langversion:5 /target:exe "/out:$OutputDirectory\StartupLoadingTests.exe" (Join-Path $PSScriptRoot 'StartupMessages.cs') (Join-Path $PSScriptRoot 'StartupLoadingTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Startup content test compilation failed.' }
& (Join-Path $OutputDirectory 'StartupLoadingTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Startup content tests failed.' }
& (Join-Path $PSScriptRoot 'build.ps1')
$process = Start-Process (Join-Path $PSScriptRoot 'dist\AstroArchive.exe') -ArgumentList @('--ui-test', "`"$OutputDirectory`"", '--startup-only', '--no-updates') -PassThru
if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Startup UI checks timed out.' }
if ($process.ExitCode -ne 0) {
    $errorFile = Join-Path $OutputDirectory 'ui-smoke-error.txt'
    if (Test-Path $errorFile) { Get-Content $errorFile }
    throw 'Startup UI checks failed.'
}
Get-Content (Join-Path $OutputDirectory 'startup-ui-passed.txt')
