param([string]$OutputDirectory = (Join-Path $env:TEMP ('AstroArchive-Remote-Protocols-' + [guid]::NewGuid().ToString('N'))))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$remote = Join-Path $PSScriptRoot 'Remote'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Copy-Item (Join-Path $remote 'lib\SMBLibrary.dll') $OutputDirectory
foreach ($name in @('Core','Smb','Discovery')) {
    $sources = if ($name -eq 'Discovery') { @("$remote\Discovery.cs", "$remote\DiscoveryTests.cs") } else { @("$remote\DownloadCore.cs", "$remote\SmbSource.cs", "$remote\${name}Tests.cs") }
    & $compiler /nologo /target:exe /langversion:5 /r:System.Web.Extensions.dll "/r:$remote\lib\SMBLibrary.dll" "/out:$OutputDirectory\${name}Tests.exe" @sources
    if ($LASTEXITCODE -ne 0) { throw "$name protocol test compilation failed." }
}
& "$OutputDirectory\CoreTests.exe" (Join-Path $OutputDirectory 'core')
if ($LASTEXITCODE -ne 0) { throw 'Remote transfer safeguards failed.' }
& "$OutputDirectory\DiscoveryTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Remote discovery checks failed.' }
$storage = Join-Path $OutputDirectory 'storage'
New-Item -ItemType Directory -Force -Path $storage | Out-Null
$smbReady = Join-Path $OutputDirectory 'smb-ready.txt'
$ftpReady = Join-Path $OutputDirectory 'ftp-ready.txt'
$smb = $null; $ftp = $null
try {
    $smb = Start-Process python -ArgumentList @(('"' + "$remote\smb-test-server.py" + '"'), ('"' + $OutputDirectory + '"'), '--port', '24445', '--ready', ('"' + $smbReady + '"')) -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'smb.out') -RedirectStandardError (Join-Path $OutputDirectory 'smb.err')
    $ftp = Start-Process python -ArgumentList @(('"' + "$remote\ftp-test-server.py" + '"'), ('"' + $storage + '"'), '24421', ('"' + $ftpReady + '"')) -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'ftp.out') -RedirectStandardError (Join-Path $OutputDirectory 'ftp.err')
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while (-not ((Test-Path $smbReady) -and (Test-Path $ftpReady))) {
        if ($smb.HasExited -or $ftp.HasExited -or $clock.Elapsed.TotalSeconds -gt 15) { Get-Content (Join-Path $OutputDirectory '*.err'); throw 'Loopback file servers did not become ready.' }
        Start-Sleep -Milliseconds 100
    }
    & "$OutputDirectory\SmbTests.exe" $OutputDirectory '24445'
    if ($LASTEXITCODE -ne 0) { throw 'Read-only guest SMB checks failed.' }
    foreach ($protocol in @('smb','ftp')) {
        $port = if ($protocol -eq 'smb') { '24445' } else { '24421' }
        & "$PSScriptRoot\AstroArchiveTests.exe" $OutputDirectory "--remote-$protocol" $port
        if ($LASTEXITCODE -ne 0) { throw "Real $protocol archive import failed." }
    }
} finally {
    foreach ($process in @($smb,$ftp)) { if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
}
