# Response files avoid Windows' command-line limit in long worktree paths.
function Invoke-AstroCompiler {
    param(
        [Parameter(Mandatory=$true,Position=0)][string]$CompilerPath,
        [Parameter(ValueFromRemainingArguments=$true)][string[]]$CompilerArguments
    )
    $responseDirectory = Join-Path $PSScriptRoot '.build'
    New-Item -ItemType Directory -Force -Path $responseDirectory | Out-Null
    $responsePath = Join-Path $responseDirectory ('compile-' + [Guid]::NewGuid().ToString('N') + '.rsp')
    $lines = @($CompilerArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' })
    [IO.File]::WriteAllLines($responsePath, $lines, [Text.UTF8Encoding]::new($true))
    & $CompilerPath ("@" + $responsePath)
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed; arguments retained in $responsePath" }
}
