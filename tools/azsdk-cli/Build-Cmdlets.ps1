#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the AzSdk CLI binary cmdlets module.

.DESCRIPTION
    This script builds the C# cmdlet project and copies the compiled DLL
    to the PowerShell module directory for distribution.

.EXAMPLE
    ./Build-Cmdlets.ps1
#>

param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "AzSdkCli.Cmdlets"
$OutputDir = Join-Path $ScriptDir "PowerShell"

Write-Host "Building AzSdk CLI Cmdlets..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Build the project
Push-Location $ProjectDir
try {
    $buildOutput = dotnet build --configuration $Configuration 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:`n$buildOutput"
        exit 1
    }
    Write-Host "Build succeeded" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Find the built DLL - check artifacts directory first (standard build location)
$artifactsDir = Join-Path $ScriptDir ".." ".." "artifacts"
if (Test-Path $artifactsDir) {
    $dllPath = Join-Path $artifactsDir "bin" "AzSdkCli.Cmdlets" $Configuration "net8.0" "AzSdkCli.Cmdlets.dll"
}
else {
    # Fallback to project bin directory
    $dllPath = Join-Path $ProjectDir "bin" $Configuration "net8.0" "AzSdkCli.Cmdlets.dll"
}

if (-not (Test-Path $dllPath)) {
    Write-Error "Could not find compiled DLL at: $dllPath"
    exit 1
}

# Copy to PowerShell directory
$destinationPath = Join-Path $OutputDir "AzSdkCli.Cmdlets.dll"
Write-Host "Copying DLL to: $destinationPath" -ForegroundColor Gray
Copy-Item -Path $dllPath -Destination $destinationPath -Force

Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "To use the cmdlets, run:" -ForegroundColor Cyan
Write-Host "  Import-Module ./PowerShell/AzSdkCli.Cmdlets.psd1" -ForegroundColor Yellow
