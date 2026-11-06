param(
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '..\release-artifacts'),
    [string]$SiteDirectory = '',
    [ValidateSet('update.json','website-update.json')][string]$ManifestName = 'update.json'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = Get-Content (Join-Path $root 'Installer\release.json') -Raw | ConvertFrom-Json
$package = "$($release.application_version).$($release.installer_revision)"
$installer = Get-Item -LiteralPath (Join-Path $ArtifactDirectory "AstroArchive$package.exe")
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($installer.FullName).FileVersion -ne $package) { throw 'Installer version differs from release.json.' }
$client = [IO.File]::ReadAllText((Join-Path $root 'Installer\Updates.cs'))
$website = [regex]::Match($client, 'public const string Website = "(https://[^"/]+)";').Groups[1].Value
if (!$website) { throw 'Cannot determine the updater website.' }
$hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    schema=1; application_version=$release.application_version; package_version=$package
    filename=$installer.Name; authenticode_signed=((Get-AuthenticodeSignature $installer.FullName).Status -eq 'Valid')
    url="$website/downloads/$($installer.Name)"; download_url="$website/downloads/$($installer.Name)"
    release_notes=[IO.File]::ReadAllText((Join-Path $root 'Installer\Payload\Release_Notes.txt'))
    sha256=$hash; size=$installer.Length; published_at=[DateTime]::UtcNow.ToString('o')
}
$json = $manifest | ConvertTo-Json -Depth 5
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $ArtifactDirectory $ManifestName), $json, $utf8)
[IO.File]::WriteAllText((Join-Path $ArtifactDirectory 'release.js'), "export const releaseManifest = $json;`n", $utf8)
[IO.File]::WriteAllText("$($installer.FullName).sha256", "$hash  $($installer.Name)`n", $utf8)
if ($SiteDirectory) { Copy-Item -LiteralPath (Join-Path $ArtifactDirectory 'release.js') -Destination (Join-Path $SiteDirectory 'worker\release.js') -Force }
Write-Output "Prepared website feed for $package ($($installer.Length) bytes)."
