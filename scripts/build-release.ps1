# Run with Windows PowerShell 5.1 from any directory.
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\release-artifacts'),
    [ValidateSet('All', 'Prepare', 'Package', 'Finalize')][string]$Stage = 'All',
    [switch]$RequireSigned,
    [switch]$Portable,
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
# Signing changes bytes: refresh the single hybrid installer's checksum.
$installerHash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$installer.sha256" "$installerHash  $([IO.Path]::GetFileName($installer))" -Encoding ASCII
# Remove stale generated aliases when reusing an artifact directory. Historical releases are retained.
Get-ChildItem $OutputDirectory -Filter 'AstroArchive-*-Windows-x64-Offline-Setup.exe*' | Remove-Item -Force
$feed = [ordered]@{
    schema = 1
    application_version = $version
    package_version = $package
    authenticode_signed = [bool]$RequireSigned
    url = "https://github.com/arijguest/AstroArchive/releases/download/$tag/$([IO.Path]::GetFileName($installer))"
    download_url = "https://github.com/arijguest/AstroArchive/releases/download/$tag/$([IO.Path]::GetFileName($installer))"
    release_notes = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\Installer\Payload\Release_Notes.txt'))
    sha256 = $installerHash
    size = (Get-Item $installer).Length
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'update.json'), ($feed | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
& (Join-Path $PSScriptRoot 'write-website-feed.ps1') -ArtifactDirectory $OutputDirectory -ManifestName 'website-update.json'
if ($Portable) {
    $zip = Join-Path $OutputDirectory "AstroArchive$package-Windows-x64-Portable.zip"
    $portableFiles = Get-ChildItem -LiteralPath $payload -File | Where-Object { $_.Name -ne 'Start.exe' }
    Compress-Archive -LiteralPath $portableFiles.FullName -DestinationPath $zip -CompressionLevel Optimal -Force
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$zip.sha256" -Value "$zipHash  $([IO.Path]::GetFileName($zip))" -Encoding ASCII
}
Copy-Item (Join-Path $root 'Installer\Payload\Release_Notes.txt') (Join-Path $OutputDirectory 'Release_Notes.txt') -Force

# Windows-only smoke: real registration/shortcuts, repair, uninstall and WPF rendering.
# The runner uses a disposable account; no real captures are imported.
$smokeId = [Guid]::NewGuid().ToString('N')
$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) ('AstroArchive-smoke-' + $smokeId)
$restarted = $null
$installedApp = $null
$installed = $false
$seededChoice = $null
$seededSettings = $null
$pinnedFixture = Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\AstroArchive-smoke-' + [Guid]::NewGuid().ToString('N') + '.lnk')
$smokeStarted = Get-Date
function Shortcut-Target([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($Path)
    try { return $link.TargetPath } finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
}
function Run-Checked([string]$File, [string[]]$Arguments, [int]$ExpectedExit = 0) {
    Write-Output ("Smoke: " + [IO.Path]::GetFileName($File) + " " + ($Arguments -join ' '))
    if ($Arguments -contains '--ui-test') {
        # The disposable runner needs a rendered window for its native UI checks.
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = $File
        $info.Arguments = $Arguments -join ' '
        $info.UseShellExecute = $false
        $process = [Diagnostics.Process]::Start($info)
    } else {
        $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    }
    # Wait for setup itself; PowerShell -Wait also waits for the restarted app.
    if ($Arguments -contains '--ui-test') {
        $uiClock = [Diagnostics.Stopwatch]::StartNew()
        $lastUiStage = ''
        $stageFile = Join-Path $OutputDirectory 'ui-preview\ui-current-stage.txt'
        while (-not $process.WaitForExit(1000)) {
            if (Test-Path $stageFile) {
                $uiStage = [IO.File]::ReadAllText($stageFile)
                if ($uiStage -ne $lastUiStage) { Write-Output ("Windows UI: " + $uiStage); $lastUiStage = $uiStage }
            }
            if ($uiClock.Elapsed.TotalMinutes -ge 5) { $process.Kill(); throw ("Windows UI smoke did not complete within five minutes. Last stage: " + $lastUiStage) }
        }
    } elseif (-not $process.WaitForExit(180000)) {
        # A hidden modal dialog would otherwise stall the job until its timeout. Name it.
        Get-Process | Where-Object { $_.MainWindowTitle } | ForEach-Object { Write-Output ("Open window: " + $_.ProcessName + " [" + $_.Id + "] " + $_.MainWindowTitle) }
        Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'AstroArchive|Start\.exe' } | ForEach-Object { Write-Output ("Process: [" + $_.ProcessId + "] parent " + $_.ParentProcessId + " " + $_.CommandLine) }
        $setupError = Join-Path ([IO.Path]::GetTempPath()) 'AstroArchive-setup-error.txt'
        if (Test-Path $setupError) { Get-Content $setupError | Write-Output }
        $process.Kill()
        throw "$File did not finish within three minutes."
    }
    if ($process.ExitCode -ne $ExpectedExit) {
        $smokeError = Join-Path $OutputDirectory 'ui-preview\ui-smoke-error.txt'
        if (Test-Path $smokeError) { Get-Content $smokeError | Write-Output }
        $setupError = Join-Path ([IO.Path]::GetTempPath()) 'AstroArchive-setup-error.txt'
        if (Test-Path $setupError) { Get-Content $setupError | Write-Output }
        Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-2)} -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -eq '.NET Runtime' -and $_.Message -match 'AstroArchive' } |
            ForEach-Object { $_.Message | Write-Output }
        throw "$File exited with $($process.ExitCode); expected $ExpectedExit."
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
    # Windows must be able to start the installed launcher and application.
    Run-Checked $installer @('--verify-launch', '--silent', '--root', ('"' + $smokeRoot + '"'))
    # Simulate Windows refusing to start an update (as Smart App Control did for 3.1.6):
    # setup must restore the newest earlier installed version and repoint its shortcuts.
    Write-Output 'Smoke: preparing a simulated blocked launcher'
    $active = $record.ActiveDirectory
    $fallback = 'app-0.0.1-r1'
    $blockedRecord = Get-Content (Join-Path $smokeRoot 'install.json') -Raw | ConvertFrom-Json
    [IO.Directory]::CreateDirectory((Join-Path $smokeRoot $fallback)) | Out-Null
    $fallbackFiles = @()
    foreach ($relative in @($blockedRecord.Files)) {
        if ($relative.Split('\/')[0] -ne $active) { continue }
        $name = Split-Path $relative -Leaf
        Copy-Item (Join-Path $smokeRoot $relative) (Join-Path $smokeRoot "$fallback\$name")
        $fallbackFiles += "$fallback\$name"
    }
    $blockedRecord.Files = @($blockedRecord.Files) + $fallbackFiles
    [IO.File]::WriteAllText((Join-Path $smokeRoot 'install.json'), ($blockedRecord | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
    [IO.File]::WriteAllText((Join-Path $smokeRoot "$active\Start.exe"), 'Simulated launcher that Windows refuses to start')
    Run-Checked $installer @('--verify-launch', '--silent', '--root', ('"' + $smokeRoot + '"')) 3
    $restored = Get-Content (Join-Path $smokeRoot 'install.json') -Raw | ConvertFrom-Json
    if ($restored.ActiveDirectory -ne $fallback) { throw 'A blocked launcher did not restore the earlier installed version.' }
    Write-Output 'Smoke: checking restored record and shortcuts'
    $desktopLink = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'AstroArchive.lnk'
    if ((Shortcut-Target $desktopLink) -ne (Join-Path $smokeRoot "$fallback\Start.exe")) { throw 'Shortcuts still open the blocked version.' }
    $block = Get-Content (Join-Path $smokeRoot 'launch-blocked.json') -Raw | ConvertFrom-Json
    if (@($block.BlockedPackages) -notcontains $package -or -not $block.NoticePending) { throw 'The blocked package was not recorded for the update check and notice.' }
    # A working reinstall of the same package becomes active again and is no longer suppressed.
    Run-Checked $installer @('--silent', '--root', ('"' + $smokeRoot + '"'))
    $repaired = Get-Content (Join-Path $smokeRoot 'install.json') -Raw | ConvertFrom-Json
    if ($repaired.ActiveDirectory -ne $active -or (Shortcut-Target $desktopLink) -ne (Join-Path $smokeRoot "$active\Start.exe")) { throw 'Repair did not reactivate the working package.' }
    if (Test-Path (Join-Path $smokeRoot 'launch-blocked.json')) { throw 'A package that starts again is still suppressed.' }
    Write-Output 'PASS launch verification and automatic rollback from a blocked update'
    # Exercise the native update handoff, process wait and offline restart.
    # A fresh profile makes the restarted app ask for Hybrid/Offline in a modal dialog the smoke cannot close;
    # record an Offline choice, as a user would have, so the restart opens the main window only.
    $choicePath = Join-Path $env:LOCALAPPDATA 'AstroArchive\connectivity.json'
    if (-not (Test-Path $choicePath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $choicePath) | Out-Null
        [IO.File]::WriteAllText($choicePath, '{"Schema":1,"Mode":2}', (New-Object Text.UTF8Encoding($false)))
        $seededChoice = $choicePath
    }
    # A profile without settings.json is a first run: the restarted app would open the modal location prompt. Record it as answered.
    $settingsPath = Join-Path $env:LOCALAPPDATA 'AstroArchive\settings.json'
    if (-not (Test-Path $settingsPath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath) | Out-Null
        [IO.File]::WriteAllText($settingsPath, '{"LocationPromptSeen":true,"GuideSeen":true}', (New-Object Text.UTF8Encoding($false)))
        $seededSettings = $settingsPath
    }
    $waitProcess = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 5') -PassThru -WindowStyle Hidden
    $handoffClock = [Diagnostics.Stopwatch]::StartNew()
    $previousProbe = $env:ASTROARCHIVE_RESTART_PROBE
    $env:ASTROARCHIVE_RESTART_PROBE = $smokeId
    try {
        Run-Checked $installer @('--update', '--silent', '--root', ('"' + $smokeRoot + '"'), '--waitpid', $waitProcess.Id, '--restart')
    } finally {
        $env:ASTROARCHIVE_RESTART_PROBE = $previousProbe
    }
    $handoffClock.Stop()
    if ($handoffClock.Elapsed.TotalSeconds -lt 2) { throw 'Installer did not wait for the handoff process.' }
    $readyFile = Join-Path $smokeRoot 'restart-ready.txt'
    $launchClock = [Diagnostics.Stopwatch]::StartNew()
    $windowHandle = [IntPtr]::Zero
    while ($launchClock.Elapsed.TotalSeconds -lt 40) {
        if (Test-Path -LiteralPath $readyFile) {
            $ready = [IO.File]::ReadAllText($readyFile).Trim() -split ' '
            if ($ready.Count -eq 2 -and $ready[0] -match '^\d+$' -and $ready[1] -match '^\d+$') {
                $restarted = Get-Process -Id ([int]$ready[0]) -ErrorAction SilentlyContinue
                $windowHandle = [IntPtr]([long]$ready[1])
                if ($null -eq $restarted -or $restarted.Path -ne $installedApp) {
                    throw 'The process that signalled restart readiness is no longer running from the smoke installation.'
                }
                $restarted.Refresh()
                if ($windowHandle -ne [IntPtr]::Zero -and $restarted.MainWindowHandle -eq $windowHandle) { break }
            }
        }
        Start-Sleep -Milliseconds 150
    }
    if ($null -eq $restarted -or $restarted.MainWindowHandle -ne $windowHandle -or $windowHandle -eq [IntPtr]::Zero) {
        throw 'The restarted application did not render its main window.'
    }
    if ([IO.File]::ReadAllText($fixture) -ne 'preserve this fixture') { throw 'Update handoff changed archive fixture data.' }
    if (-not $restarted.CloseMainWindow() -or -not $restarted.WaitForExit(10000)) { throw 'Restarted application did not close cleanly.' }
    $launcherPath = Join-Path $smokeRoot ($record.ActiveDirectory + '\Start.exe')
    foreach ($launcher in @(Get-Process -Name 'Start' -ErrorAction SilentlyContinue)) {
        if ($launcher.Path -eq $launcherPath -and -not $launcher.WaitForExit(10000)) { throw 'Restarted launcher remained open.' }
    }
    # Process exit can precede Windows releasing executable/scanner handles.
    # Wait for the same exclusive access uninstall requires, without retrying
    # uninstall itself or weakening its running-application checks.
    $releaseClock = [Diagnostics.Stopwatch]::StartNew()
    foreach ($relativeFile in $record.Files) {
        $managedFile = Join-Path $smokeRoot $relativeFile
        while (Test-Path $managedFile) {
            try {
                $handle = [IO.File]::Open($managedFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
                $handle.Dispose()
                break
            } catch [IO.IOException] {
                $nativeCode = $_.Exception.GetBaseException().HResult -band 0xFFFF
                if ($nativeCode -notin @(32, 33) -or $releaseClock.Elapsed.TotalSeconds -ge 10) { throw }
                Start-Sleep -Milliseconds 100
            }
        }
    }
    Write-Output 'PASS native update handoff, process wait, offline restart and archive preservation'
    # Fail the release if Windows Code Integrity rejected any AstroArchive executable during validation.
    $integrity = @(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-CodeIntegrity/Operational'; Id=3033,3034,3076,3077; StartTime=$smokeStarted} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'AstroArchive|Start\.exe' })
    $integrity | ForEach-Object { Write-Output ("Code Integrity event " + $_.Id + ": " + $_.Message) }
    if (@($integrity | Where-Object { $_.Id -in @(3033, 3077) }).Count -gt 0) { throw 'Windows Code Integrity blocked an AstroArchive executable during validation.' }
    Write-Output 'PASS no Windows Code Integrity blocks for the installed executables'
    Run-Checked $installer @('--uninstall', '--silent', '--root', ('"' + $smokeRoot + '"'))
    $installed = $false
    if ((Test-Path (Join-Path $smokeRoot 'install.json')) -or -not (Test-Path $fixture)) { throw 'Uninstall smoke failed.' }
} finally {
    if (Test-Path $pinnedFixture) { Remove-Item $pinnedFixture -Force }
    if ($seededChoice -and (Test-Path $seededChoice)) { Remove-Item $seededChoice -Force }
    if ($seededSettings -and (Test-Path $seededSettings)) { Remove-Item $seededSettings -Force }
    # Cleanup must preserve the first failure, even when a test executable is still open.
    if ($installedApp) {
        foreach ($candidate in @(Get-Process -Name 'AstroArchive' -ErrorAction SilentlyContinue)) {
            try {
                if ($candidate.Path -eq $installedApp) {
                    $null = $candidate.CloseMainWindow()
                    if (-not $candidate.WaitForExit(5000)) { $candidate.Kill(); $null = $candidate.WaitForExit(5000) }
                }
            } catch { Write-Warning "Smoke process cleanup failed: $_" }
        }
    }
    if ($installed) { try { Run-Checked $installer @('--uninstall', '--silent', '--root', ('"' + $smokeRoot + '"')) } catch { Write-Warning "Cleanup uninstall failed: $_" } }
    if (Test-Path -LiteralPath $smokeRoot) {
        try {
            $resolved = (Resolve-Path -LiteralPath $smokeRoot).ProviderPath
            $expected = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('AstroArchive-smoke-' + $smokeId)))
            if ($resolved -ne $expected -or ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Unexpected smoke cleanup path.'
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        } catch { Write-Warning "Smoke directory cleanup failed: $_" }
    }
}
Write-Output "Validated Windows release $tag"
