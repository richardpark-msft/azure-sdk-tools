<#
.SYNOPSIS
    Validates an Azure SDK package.

.DESCRIPTION
    Validates an Azure SDK package by checking for required files like README.md and CHANGELOG.md.

.PARAMETER PackagePath
    The path to the package directory or file to validate.

.EXAMPLE
    Test-AzSdkPackage -PackagePath "./my-package"

.OUTPUTS
    Returns $true if validation passes, $false otherwise.
#>
function Test-AzSdkPackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$PackagePath
    )

    if (-not (Test-Path $PackagePath)) {
        Write-Error "Error: Package path '$PackagePath' does not exist"
        return $false
    }

    Write-Host "Validating package: $PackagePath"
    
    $isValid = $true
    
    if (Test-Path $PackagePath -PathType Container) {
        $files = Get-ChildItem -Path $PackagePath -Recurse -File
        Write-Host "Found $($files.Count) files"
        
        # Check for required files
        $hasReadme = $files | Where-Object { $_.Name -eq "README.md" }
        $hasChangelog = $files | Where-Object { $_.Name -eq "CHANGELOG.md" }
        
        if (-not $hasReadme) {
            Write-Warning "README.md not found"
            $isValid = $false
        }
        if (-not $hasChangelog) {
            Write-Warning "CHANGELOG.md not found"
            $isValid = $false
        }
    }

    if ($isValid) {
        Write-Host "Package validation passed"
        return $true
    }
    else {
        Write-Host "Package validation failed"
        return $false
    }
}

<#
.SYNOPSIS
    Lists files in a directory.

.DESCRIPTION
    Lists files in a directory with optional pattern matching.

.PARAMETER Path
    The directory path to list files from.

.PARAMETER Pattern
    Optional pattern to filter files (default: *.*).

.EXAMPLE
    Get-AzSdkFiles -Path "." -Pattern "*.cs"

.OUTPUTS
    Returns an array of file information objects.
#>
function Get-AzSdkFiles {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string]$Path = ".",

        [Parameter(Position = 1)]
        [string]$Pattern = "*.*"
    )

    if (-not (Test-Path $Path -PathType Container)) {
        Write-Error "Error: Directory '$Path' does not exist"
        return
    }

    Write-Host "Listing files in: $Path"
    Write-Host "Pattern: $Pattern"
    Write-Host ""

    $files = Get-ChildItem -Path $Path -Filter $Pattern -File
    
    foreach ($file in $files) {
        Write-Host "$($file.Name) ($($file.Length) bytes)"
    }

    Write-Host ""
    Write-Host "Total: $($files.Count) file(s)"
    
    return $files
}

<#
.SYNOPSIS
    Generates metadata for an Azure SDK package.

.DESCRIPTION
    Generates a JSON metadata file containing package information.

.PARAMETER PackageName
    The name of the package.

.PARAMETER Version
    The version of the package.

.PARAMETER OutputPath
    The output path for the metadata file (default: metadata.json).

.EXAMPLE
    New-AzSdkMetadata -PackageName "Azure.Core" -Version "1.0.0" -OutputPath "metadata.json"

.OUTPUTS
    Returns the path to the generated metadata file.
#>
function New-AzSdkMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$PackageName,

        [Parameter(Mandatory = $true, Position = 1)]
        [string]$Version,

        [Parameter(Position = 2)]
        [string]$OutputPath = "metadata.json"
    )

    $metadata = @{
        packageName = $PackageName
        version = $Version
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        tool = "azsdk-cli"
        toolVersion = "1.0.0"
    }

    $json = $metadata | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath $OutputPath -Encoding utf8

    Write-Host "Metadata generated: $OutputPath"
    Write-Host $json
    
    return $OutputPath
}

<#
.SYNOPSIS
    Shows help information for AzSdk CLI functions.

.DESCRIPTION
    Displays help information for all available AzSdk CLI PowerShell functions.

.EXAMPLE
    Show-AzSdkHelp
#>
function Show-AzSdkHelp {
    Write-Host "AzSdk CLI - Azure SDK Command Line Interface (PowerShell)"
    Write-Host ""
    Write-Host "Available Functions:"
    Write-Host "  Test-AzSdkPackage <package-path>              Validate an Azure SDK package"
    Write-Host "  Get-AzSdkFiles <path> [pattern]               List files in a directory"
    Write-Host "  New-AzSdkMetadata <name> <version> [out]      Generate package metadata"
    Write-Host "  Show-AzSdkHelp                                Show this help message"
    Write-Host ""
    Write-Host "For detailed help on any function, use: Get-Help <function-name> -Detailed"
    Write-Host ""
}

# Export functions
Export-ModuleMember -Function Test-AzSdkPackage, Get-AzSdkFiles, New-AzSdkMetadata, Show-AzSdkHelp
