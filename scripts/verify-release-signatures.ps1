param(
    [Parameter(Mandatory=$true)][string[]]$Paths,
    [Parameter(Mandatory=$true)][string]$ExpectedPublisher,
    [string]$ReportPath = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ExpectedPublisher)) { throw 'Set the expected signing certificate subject before publishing.' }
if ($Paths.Count -eq 0) { throw 'No executables supplied for signature verification.' }
$records = @(
    foreach ($path in $Paths) {
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        if ($signature.Status -ne 'Valid') { throw "Invalid Authenticode signature on $path ($($signature.Status))." }
        if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne $ExpectedPublisher) {
            throw "Unexpected signing publisher on $path."
        }
        if ($null -eq $signature.TimeStamperCertificate) { throw "Missing trusted timestamp on $path." }
        [ordered]@{
            file = [IO.Path]::GetFileName($path)
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            status = 'Valid'
            publisher = $signature.SignerCertificate.Subject
            thumbprint = $signature.SignerCertificate.Thumbprint
            timestamp_publisher = $signature.TimeStamperCertificate.Subject
        }
    }
)
if ($ReportPath) {
    $report = [ordered]@{ schema = 1; files = $records }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath), ($report | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
}
Write-Output "PASS trusted, timestamped signatures from $ExpectedPublisher ($($records.Count) executables)"
