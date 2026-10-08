# Run with Windows PowerShell 5.1 from any directory.
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\release-artifacts'),
    [ValidateSet('All', 'Prepare', 'Package', 'Finalize')][string]$Stage = 'All',
    [switch]$RequireSigned,
    [string]$ExpectedPublisher = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = Get-Content (Join-Path $root 'Installer\release.json') -Raw | ConvertFrom-Json
$version = $release.application_version
$revision = [int]$release.installer_revision
$package = "$version.$revision"
$tag = "v$package"
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$app = Join-Path $root 'Application_Source\dist\AstroArchive.exe'
$payload = Join-Path $root 'Installer\.build\payload'
if ($Stage -in @('All', 'Prepare')) {
    & (Join-Path $PSScriptRoot 'test-release-signatures.ps1')
    & (Join-Path $root 'Application_Source\test.ps1')
    & (Join-Path $root 'Application_Source\build.ps1')
    & (Join-Path $root 'Installer\build.ps1') -AppExecutable $app -OutputDirectory $OutputDirectory -PreparePayloadOnly
    if ($Stage -eq 'Prepare') { return }
}
if ($Stage -in @('All', 'Package')) {
    if ($RequireSigned) {
        & (Join-Path $PSScriptRoot 'verify-release-signatures.ps1') -Paths @((Join-Path $payload 'AstroArchive.exe'), (Join-Path $payload 'Start.exe')) -ExpectedPublisher $ExpectedPublisher
    }
    & (Join-Path $root 'Installer\build.ps1') -AppExecutable $app -OutputDirectory $OutputDirectory -UsePreparedPayload -SkipTests
    if ($Stage -eq 'Package') { return }
}
$installer = Join-Path $OutputDirectory "AstroArchive$package.exe"
if ($RequireSigned) {
    & (Join-Path $PSScriptRoot 'verify-release-signatures.ps1') -Paths @($installer, (Join-Path $payload 'AstroArchive.exe'), (Join-Path $payload 'Start.exe')) -ExpectedPublisher $ExpectedPublisher -ReportPath (Join-Path $OutputDirectory 'signatures.json')
} elseif (Test-Path (Join-Path $OutputDirectory 'signatures.json')) {
    Remove-Item (Join-Path $OutputDirectory 'signatures.json')
}
# Signing changes bytes: refresh checksums and the compatibility copy afterwards.
$installerHash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$installer.sha256" "$installerHash  $([IO.Path]::GetFileName($installer))" -Encoding ASCII
# Keep the asset name expected by 1.2.0 launchers so existing users can upgrade.
$compatibility = Join-Path $OutputDirectory "AstroArchive-$version-Windows-x64-Offline-Setup.exe"
Copy-Item $installer $compatibility -Force
$compatibilityHash = (Get-FileHash $compatibility -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$compatibility.sha256" "$compatibilityHash  $([IO.Path]::GetFileName($compatibility))" -Encoding ASCII
$feed = [ordered]@{
    schema = 1
    application_version = $version
    package_version = $package
    authenticode_signed = [bool]$RequireSigned
    url = "https://github.com/arijguest/AstroArchive/releases/download/$tag/$([IO.Path]::GetFileName($compatibility))"
    download_url = "https://github.com/arijguest/AstroArchive/releases/download/$tag/$([IO.Path]::GetFileName($installer))"
    release_notes = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\Installer\Payload\Release_Notes.txt'))
    sha256 = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    size = (Get-Item $installer).Length
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'update.json'), ($feed | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
Copy-Item (Join-Path $root 'Installer\Payload\Release_Notes.txt') (Join-Path $OutputDirectory 'Release_Notes.txt') -Force

# Windows-only smoke: real registration/shortcuts, repair, uninstall and WPF rendering.
# The runner uses a disposable account; no real captures are imported.
$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) ('AstroArchive-smoke-' + [Guid]::NewGuid().ToString('N'))
$installed = $false
$pinnedFixture = Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\AstroArchive-smoke-' + [Guid]::NewGuid().ToString('N') + '.lnk')
function Run-Checked([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru
    # Wait for setup itself; PowerShell -Wait also waits for the restarted app.
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $smokeError = Join-Path $OutputDirectory 'ui-preview\ui-smoke-error.txt'
        if (Test-Path $smokeError) { Get-Content $smokeError | Write-Output }
        $setupError = Join-Path ([IO.Path]::GetTempPath()) 'AstroArchive-setup-error.txt'
        if (Test-Path $setupError) { Get-Content $setupError | Write-Output }
        Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-2)} -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -eq '.NET Runtime' -and $_.Message -match 'AstroArchive' } |
            ForEach-Object { $_.Message | Write-Output }
        throw "$File exited with $($process.ExitCode)."
    }
}
try {
    # Simulate a pin whose versioned executable/icon disappeared during an update.
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($pinnedFixture)) | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($pinnedFixture)
    try {
        $shortcut.TargetPath = Join-Path $smokeRoot 'app-0.0.0-r1\Start.exe'
        $shortcut.IconLocation = $shortcut.TargetPath + ',0'
        $shortcut.Save()
    } finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
    Run-Checked $installer @('--silent', '--root', ('"' + $smokeRoot + '"'))
    $installed = $true
    $record = Get-Content (Join-Path $smokeRoot 'install.json') -Raw | ConvertFrom-Json
    if ($record.PackageVersion -ne $package) { throw 'Smoke installation version mismatch.' }
    $installedApp = Join-Path $smokeRoot ($record.ActiveDirectory + '\AstroArchive.exe')
    if ($RequireSigned) {
        & (Join-Path $PSScriptRoot 'verify-release-signatures.ps1') -Paths @($installedApp, (Join-Path $smokeRoot ($record.ActiveDirectory + '\Start.exe')), (Join-Path $smokeRoot 'Uninstall.exe')) -ExpectedPublisher $ExpectedPublisher
    }
    $preview = Join-Path $OutputDirectory 'ui-preview'
    Run-Checked $installedApp @('--ui-test', ('"' + $preview + '"'))
    if (-not (Test-Path $pinnedFixture)) { throw 'Installer removed the existing taskbar pin.' }
    Write-Output 'PASS taskbar pin repair, stable shell identity and bundled logo cache'
    Run-Checked $installer @('--ui-test', ('"' + $preview + '"'), '--root', ('"' + $smokeRoot + '"'))
    if (-not (Test-Path (Join-Path $preview 'AstroArchive_Installer_UI.png'))) { throw 'Installer UI smoke did not render.' }
    if (-not (Get-ChildItem $preview -Filter '*.png')) { throw 'UI smoke did not render images.' }
    $fixtureDirectory = Join-Path $smokeRoot 'repository'
    [IO.Directory]::CreateDirectory($fixtureDirectory) | Out-Null
    $fixture = Join-Path $fixtureDirectory 'keep.txt'
    [IO.File]::WriteAllText($fixture, 'preserve this fixture')
    Run-Checked $installer @('--silent', '--root', ('"' + $smokeRoot + '"'))
    if ([IO.File]::ReadAllText($fixture) -ne 'preserve this fixture') { throw 'Repair changed fixture data.' }
    # Exercise the native update handoff, process wait and offline restart.
    $waitProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 5') -PassThru
    $handoffClock = [Diagnostics.Stopwatch]::StartNew()
    Run-Checked $installer @('--update', '--silent', '--root', ('"' + $smokeRoot + '"'), '--waitpid', $waitProcess.Id, '--restart')
    $handoffClock.Stop()
    if ($handoffClock.Elapsed.TotalSeconds -lt 2) { throw 'Installer did not wait for the handoff process.' }
    $restarted = $null
    $launchClock = [Diagnostics.Stopwatch]::StartNew()
    while ($null -eq $restarted -and $launchClock.Elapsed.TotalSeconds -lt 15) {
        foreach ($candidate in @(Get-Process -Name 'AstroArchive' -ErrorAction SilentlyContinue)) {
            if ($candidate.Path -eq $installedApp -and $candidate.MainWindowHandle -ne 0) { $restarted = $candidate; break }
        }
        if ($null -eq $restarted) { Start-Sleep -Milliseconds 200 }
    }
    if ($null -eq $restarted) { throw 'Update handoff did not restart the application.' }
    if ([IO.File]::ReadAllText($fixture) -ne 'preserve this fixture') { throw 'Update handoff changed archive fixture data.' }
    if (-not $restarted.CloseMainWindow() -or -not $restarted.WaitForExit(10000)) { throw 'Restarted application did not close cleanly.' }
    $launcherPath = Join-Path $smokeRoot ($record.ActiveDirectory + '\Start.exe')
    foreach ($launcher in @(Get-Process -Name 'Start' -ErrorAction SilentlyContinue)) {
        if ($launcher.Path -eq $launcherPath -and -not $launcher.WaitForExit(10000)) { throw 'Restarted launcher remained open.' }
    }
    Write-Output 'PASS native update handoff, process wait, offline restart and archive preservation'
    Run-Checked $installer @('--uninstall', '--silent', '--root', ('"' + $smokeRoot + '"'))
    $installed = $false
    if ((Test-Path (Join-Path $smokeRoot 'install.json')) -or -not (Test-Path $fixture)) { throw 'Uninstall smoke failed.' }
} finally {
    if (Test-Path $pinnedFixture) { Remove-Item $pinnedFixture -Force }
    if ($installed) { Run-Checked $installer @('--uninstall', '--silent', '--root', ('"' + $smokeRoot + '"')) }
    if (Test-Path $smokeRoot) { Remove-Item $smokeRoot -Recurse -Force }
}
Write-Output "Validated Windows release $tag"
