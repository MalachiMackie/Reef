$ErrorActionPreference = "Stop"

$OutputDir = Join-Path $PSScriptRoot "build"

if (!(Test-Path $OutputDir))
{
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Locate Visual Studio
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (!(Test-Path $vswhere))
{
    throw "vswhere.exe not found"
}

$vsInstall = & $vswhere `
    -latest `
    -products * `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath

if (-not $vsInstall)
{
    throw "Visual Studio C++ tools not found"
}

$vcvars = Join-Path $vsInstall "VC\Auxiliary\Build\vcvars64.bat"

if (!(Test-Path $vcvars))
{
    throw "vcvars64.bat not found"
}

$commands = @"
call "$vcvars"
cd /d "$OutputDir"

cl.exe /nologo /std:c11 /c /Zi /Fd:reef-runtime.pdb "$PSScriptRoot\reef-runtime.c"
if errorlevel 1 exit /b %errorlevel%

lib.exe /nologo /OUT:reef-runtime.lib reef-runtime.obj
if errorlevel 1 exit /b %errorlevel%
"@

$tempBat = Join-Path $env:TEMP "reef-build-$PID.bat"
Set-Content -Path $tempBat -Value $commands -Encoding ASCII

try
{
    & cmd.exe /c $tempBat

    if ($LASTEXITCODE -ne 0)
    {
        throw "Build failed"
    }

    Write-Host "Built $OutputDir\reef-runtime.lib"
    Write-Host "PDB   $OutputDir\reef-runtime.pdb"
} finally
{
    Remove-Item $tempBat -ErrorAction SilentlyContinue
}
