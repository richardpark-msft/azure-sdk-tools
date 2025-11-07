#!/usr/bin/env pwsh
<#
.SYNOPSIS
    AzSdk CLI - Command line wrapper for the AzSdkCli PowerShell module

.DESCRIPTION
    This script provides a command-line interface to the AzSdkCli PowerShell module,
    mimicking the original C# CLI tool's command structure.

.EXAMPLE
    ./azsdk-cli.ps1 help
    ./azsdk-cli.ps1 validate-package ./my-package
    ./azsdk-cli.ps1 list-files . "*.cs"
    ./azsdk-cli.ps1 generate-metadata "Azure.Core" "1.0.0"
#>

param(
    [Parameter(Position = 0)]
    [string]$Command,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModulePath = Join-Path $ScriptDir "PowerShell" "AzSdkCli.psd1"

# Import the module
Import-Module $ModulePath -Force

# Execute the command
switch ($Command) {
    "validate-package" {
        if ($Arguments.Count -lt 1) {
            Write-Error "Error: Package path required"
            Write-Host "Usage: azsdk-cli validate-package <package-path>"
            exit 1
        }
        $result = Test-AzSdkPackage -PackagePath $Arguments[0]
        exit ($result ? 0 : 1)
    }
    
    "list-files" {
        $path = if ($Arguments.Count -gt 0) { $Arguments[0] } else { "." }
        $pattern = if ($Arguments.Count -gt 1) { $Arguments[1] } else { "*.*" }
        Get-AzSdkFiles -Path $path -Pattern $pattern
        exit 0
    }
    
    "generate-metadata" {
        if ($Arguments.Count -lt 2) {
            Write-Error "Error: Package name and version required"
            Write-Host "Usage: azsdk-cli generate-metadata <package-name> <version> [output-path]"
            exit 1
        }
        $packageName = $Arguments[0]
        $version = $Arguments[1]
        $outputPath = if ($Arguments.Count -gt 2) { $Arguments[2] } else { "metadata.json" }
        New-AzSdkMetadata -PackageName $packageName -Version $version -OutputPath $outputPath
        exit 0
    }
    
    { $_ -in @("help", "--help", "-h", "") } {
        Show-AzSdkHelp
        exit 0
    }
    
    default {
        Write-Error "Error: Unknown command '$Command'"
        Write-Host "Run 'azsdk-cli help' for usage information"
        exit 1
    }
}
