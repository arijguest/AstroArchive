param([string]$Compiler = (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Compiler)) { throw '.NET Framework compiler not found.' }
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('AstroArchive-installer-tests-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
try {
    $executable = Join-Path $testDirectory 'InstallerTests.exe'
    $arguments = @('/nologo', '/langversion:5', '/target:exe', '/platform:x64', "/out:$executable",
        '/r:System.Core.dll', '/r:System.Web.Extensions.dll', "$PSScriptRoot\InstallCore.cs",
        "$PSScriptRoot\Updates.cs", "$PSScriptRoot\UpdateTests.cs", "$PSScriptRoot\Tests.cs")
    & $Compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    $results = & $executable (Join-Path $testDirectory 'fixtures')
    $testExit = $LASTEXITCODE
    $results | Write-Output
    if ($testExit -ne 0) { throw 'Installer tests failed.' }
} finally {
    # This path is always a newly generated test directory, never an installation.
    Remove-Item $testDirectory -Recurse -Force
}
