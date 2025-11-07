# AzSdk CLI - PowerShell Module

This PowerShell module provides command-line utilities for Azure SDK package operations. It is a PowerShell conversion of the original C# CLI tool.

## Installation

### Import the Module

```powershell
Import-Module ./PowerShell/AzSdkCli.psd1
```

## Available Functions

### Test-AzSdkPackage

Validates an Azure SDK package by checking for required files like README.md and CHANGELOG.md.

**Syntax:**
```powershell
Test-AzSdkPackage -PackagePath <string>
```

**Example:**
```powershell
Test-AzSdkPackage -PackagePath "./my-package"
```

**Returns:** `$true` if validation passes, `$false` otherwise.

### Get-AzSdkFiles

Lists files in a directory with optional pattern matching.

**Syntax:**
```powershell
Get-AzSdkFiles [-Path <string>] [-Pattern <string>]
```

**Examples:**
```powershell
# List all files in current directory
Get-AzSdkFiles

# List all C# files in a specific directory
Get-AzSdkFiles -Path "./src" -Pattern "*.cs"
```

**Returns:** Array of file information objects.

### New-AzSdkMetadata

Generates a JSON metadata file containing package information.

**Syntax:**
```powershell
New-AzSdkMetadata -PackageName <string> -Version <string> [-OutputPath <string>]
```

**Examples:**
```powershell
# Generate metadata with default output path
New-AzSdkMetadata -PackageName "Azure.Core" -Version "1.0.0"

# Generate metadata with custom output path
New-AzSdkMetadata -PackageName "Azure.Storage.Blobs" -Version "12.0.0" -OutputPath "./metadata.json"
```

**Returns:** Path to the generated metadata file.

### Show-AzSdkHelp

Displays help information for all available functions.

**Syntax:**
```powershell
Show-AzSdkHelp
```

**Example:**
```powershell
Show-AzSdkHelp
```

## Getting Detailed Help

For detailed help on any function, use PowerShell's built-in help system:

```powershell
Get-Help Test-AzSdkPackage -Detailed
Get-Help Get-AzSdkFiles -Examples
Get-Help New-AzSdkMetadata -Full
```

## Running Tests

This module includes comprehensive Pester tests. To run them:

```powershell
# Navigate to the Tests directory
cd ./PowerShell/Tests

# Run all tests
Invoke-Pester -Path ./AzSdkCli.Tests.ps1

# Run tests with detailed output
Invoke-Pester -Path ./AzSdkCli.Tests.ps1 -Output Detailed
```

## Comparison with C# CLI

The PowerShell module provides equivalent functionality to the C# CLI:

| C# CLI Command | PowerShell Function | Description |
|----------------|---------------------|-------------|
| `azsdk-cli validate-package <path>` | `Test-AzSdkPackage -PackagePath <path>` | Validate package |
| `azsdk-cli list-files <path> [pattern]` | `Get-AzSdkFiles -Path <path> -Pattern <pattern>` | List files |
| `azsdk-cli generate-metadata <name> <ver> [out]` | `New-AzSdkMetadata -PackageName <name> -Version <ver> -OutputPath <out>` | Generate metadata |
| `azsdk-cli help` | `Show-AzSdkHelp` | Show help |

## Requirements

- PowerShell 5.1 or later
- PowerShell Core 6.0+ recommended for cross-platform support

## Exit Codes (Return Values)

PowerShell functions return boolean values or objects instead of exit codes:

- `Test-AzSdkPackage`: Returns `$true` for success, `$false` for failure
- `Get-AzSdkFiles`: Returns array of file objects, or empty array if no matches
- `New-AzSdkMetadata`: Returns output file path on success
- `Show-AzSdkHelp`: No return value (displays help)

## Examples

### Example 1: Validate Multiple Packages

```powershell
$packages = @("./package1", "./package2", "./package3")
$results = $packages | ForEach-Object {
    [PSCustomObject]@{
        Package = $_
        Valid = Test-AzSdkPackage -PackagePath $_
    }
}
$results | Format-Table
```

### Example 2: Generate Metadata for Multiple Packages

```powershell
$packages = @(
    @{Name="Azure.Core"; Version="1.0.0"},
    @{Name="Azure.Storage.Blobs"; Version="12.0.0"}
)

$packages | ForEach-Object {
    New-AzSdkMetadata -PackageName $_.Name -Version $_.Version -OutputPath "./$($_.Name)-metadata.json"
}
```

### Example 3: Find All TypeScript Files

```powershell
$files = Get-AzSdkFiles -Path "./src" -Pattern "*.ts"
Write-Host "Found $($files.Count) TypeScript files"
$files | Select-Object Name, Length | Format-Table
```

## License

See the repository LICENSE file for details.
