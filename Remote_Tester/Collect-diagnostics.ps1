# Read-only diagnostics: does not run the tester, change policy, or unblock files.
$ErrorActionPreference = 'Continue'
$output = Join-Path ([Environment]::GetFolderPath('Desktop')) ('AstroArchive-Remote-diagnostics-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
$lines = @('AstroArchive Remote tester diagnostics', (Get-Date -Format o), '')
$lines += 'Windows:'
$lines += (Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture | Format-List | Out-String)
$lines += '.NET Framework:'
$lines += (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue | Select-Object Release, Version | Format-List | Out-String)
$exe = Join-Path $PSScriptRoot 'AstroArchive.RemoteTester.exe'
if (Test-Path $exe) {
    $lines += 'Executable signature and SHA-256:'
    $lines += (Get-AuthenticodeSignature $exe | Select-Object Status, StatusMessage | Format-List | Out-String)
    $lines += (Get-FileHash $exe -Algorithm SHA256 | Format-List | Out-String)
}
$startup = Join-Path $env:LOCALAPPDATA 'AstroArchive.RemoteTester\startup-error.txt'
if (Test-Path $startup) { $lines += 'Saved application error:'; $lines += Get-Content $startup -Raw }
$since = (Get-Date).AddDays(-2)
foreach ($log in @('Application', 'Microsoft-Windows-CodeIntegrity/Operational')) {
    $lines += ('Recent matching events in ' + $log + ':')
    try {
        $events = Get-WinEvent -FilterHashtable @{ LogName=$log; StartTime=$since } -MaxEvents 2000 -ErrorAction Stop |
            Where-Object { $_.Message -match 'AstroArchive\.RemoteTester' } |
            Select-Object -First 20 TimeCreated, Id, ProviderName, Message
        $lines += ($events | Format-List | Out-String)
    } catch { $lines += ('No readable matching event data: ' + $_.Exception.Message) }
}
$lines += 'No security settings were changed. This report may contain local paths and account names.'
$lines | Out-File -LiteralPath $output -Encoding utf8
Write-Host ('Saved: ' + $output)
Start-Process notepad.exe -ArgumentList @('"' + $output + '"')
