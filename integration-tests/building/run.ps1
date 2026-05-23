function NormalizeLineEndings([string]$text)
{
    return ($text -replace "`r`n", "`n").Trim()
}

if (Test-Path "./build")
{
    Remove-Item -Recurse "./build"
}

& 'Reef.Console' build --log-level error

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Failed to build"
    return
}

$output = & './build/main.exe'
$actualOutput = ($output -join "`n").Trim()

Write-Host $actualOutput

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Failed to run"
    return
}

$expectedOutput = @"
hi from another module
error thrown 1
7
"@


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
