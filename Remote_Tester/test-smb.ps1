# Optional developer integration checks. Runtime users do not need Python.
param([string]$OutputDirectory = (Join-Path $env:TEMP ('AstroArchive-SMB-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Copy-Item (Join-Path $PSScriptRoot 'lib\SMBLibrary.dll') $OutputDirectory
& $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/r:$PSScriptRoot\lib\SMBLibrary.dll" "/out:$OutputDirectory\SmbTests.exe" "$PSScriptRoot\DownloadCore.cs" "$PSScriptRoot\SmbSource.cs" "$PSScriptRoot\SmbTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Direct SMB test compilation failed.' }
$fixtures = Join-Path $OutputDirectory 'fixtures'
$stdout = Join-Path $OutputDirectory 'fixture-output.txt'
$stderr = Join-Path $OutputDirectory 'fixture-error.txt'
$arguments = @('-u', ('"{0}"' -f (Join-Path $PSScriptRoot 'smb-test-server.py')), ('"{0}"' -f $fixtures), '--port', '24445')
$server = Start-Process -FilePath 'python' -ArgumentList $arguments -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -WindowStyle Hidden
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path $stdout) { if ((Get-Content $stdout -Raw) -match 'READY') { break } }
        if ($server.HasExited) { throw 'The local SMB fixture exited before it was ready.' }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path $stdout) -or (Get-Content $stdout -Raw) -notmatch 'READY') { throw 'The local SMB fixture did not start.' }
    & "$OutputDirectory\SmbTests.exe" $fixtures '24445'
    if ($LASTEXITCODE -ne 0) { throw 'Direct SMB integration checks failed.' }
} finally {
    if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force }
    $server.Dispose()
}
