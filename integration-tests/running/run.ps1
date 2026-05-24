function NormalizeLineEndings([string]$text)
{
    return ($text -replace "`r`n", "`n").Trim()
}

& 'dotnet' build ../..

if (Test-Path "./build")
{
    Remove-Item -Recurse "./build"
}

$output = & 'Reef.Console' run --log-level error
$actualOutput = ($output -join "`n").Trim()

Write-Host $actualOutput

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Failed to build/run"
    return
}

$expectedOutput = @"
hi from another module
error thrown 1
7
"@.Trim()

if ((NormalizeLineEndings $actualOutput) -ne (NormalizeLineEndings $expectedOutput))
{
    Write-Error "Unexpected output"
    Write-Error $expectedOutput
    return
}

$color = [System.Console]::ForegroundColor

[System.Console]::ForegroundColor = 'Green'
Write-Host "Test Passed!"

[System.Console]::ForegroundColor = $color
