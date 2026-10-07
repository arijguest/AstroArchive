# Test the release gate with controlled Windows trust results. No keys or trust
# store changes are needed; native signature validation runs on the signed build.
$ErrorActionPreference = 'Stop'
$verifier = Join-Path $PSScriptRoot 'verify-release-signatures.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('AstroArchive-signatures-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$file = Join-Path $fixture 'setup.exe'
$report = Join-Path $fixture 'signatures.json'
[IO.File]::WriteAllText($file, 'fixture bytes')
$publisher = 'CN=Test Publisher'
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    return $signatureFixture
}
function Assert-Rejected([string]$Label, [string]$Message) {
    $rejected = $false
    try { & $verifier -Paths @($file) -ExpectedPublisher $publisher -ReportPath $report }
    catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "$Label passed the release gate." }
    if (Test-Path $report) { throw "$Label produced a verification report." }
    Write-Output "PASS reject $Label"
}
try {
    $signatureFixture = [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null; TimeStamperCertificate = $null }
    Assert-Rejected 'unsigned executable' 'Invalid Authenticode'
    $signatureFixture = [pscustomobject]@{
        Status = 'Valid'
        SignerCertificate = [pscustomobject]@{ Subject = $publisher; Thumbprint = 'TEST' }
        TimeStamperCertificate = [pscustomobject]@{ Subject = 'CN=Timestamp' }
    }
    $signatureFixture.Status = 'HashMismatch'
    Assert-Rejected 'tampered executable' 'Invalid Authenticode'
    $signatureFixture.Status = 'NotTrusted'
    Assert-Rejected 'untrusted certificate' 'Invalid Authenticode'
    $signatureFixture.Status = 'Valid'
    $signatureFixture.SignerCertificate.Subject = 'CN=Other Publisher'
    Assert-Rejected 'different publisher' 'Unexpected signing publisher'
    $signatureFixture.SignerCertificate.Subject = $publisher
    $signatureFixture.TimeStamperCertificate = $null
    Assert-Rejected 'missing timestamp' 'Missing trusted timestamp'
    $signatureFixture.TimeStamperCertificate = [pscustomobject]@{ Subject = 'CN=Timestamp' }
    & $verifier -Paths @($file) -ExpectedPublisher $publisher -ReportPath $report
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if ($result.files.Count -ne 1 -or $result.files[0].publisher -ne $publisher -or
        $result.files[0].sha256 -ne (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw 'Signature report does not describe the verified bytes.'
    }
    Write-Output 'PASS record verified publisher and final file hash'
} finally {
    Remove-Item $fixture -Recurse -Force
}
