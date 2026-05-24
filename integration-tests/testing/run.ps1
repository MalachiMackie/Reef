function NormalizeLineEndings([string]$text)
{
    return ($text -replace "`r`n", "`n").Trim()
}

& 'dotnet' build ../..

if (Test-Path "./build-test")
{
    Remove-Item -Recurse "./build-test"
}

$output = & 'Reef.Console' test --log-level error
$actualOutput = ($output -join "`n").Trim()

Write-Host $actualOutput

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Failed to test"
    return
}

$expectedOutput = @"
main:::add_should_return_sum - Passed
main:::add_should_return_sum_2 - Failed

Summary:
Total: 2, Passed: 1, Failed: 1

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
