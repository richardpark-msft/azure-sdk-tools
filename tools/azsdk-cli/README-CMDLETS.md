# AzSdk CLI - PowerShell Binary Cmdlets

This directory contains compiled PowerShell cmdlets for Azure SDK package operations. The cmdlets are implemented in C# and compiled into a binary module.

## Installation

### Import the Module

```powershell
Import-Module ./PowerShell/AzSdkCli.Cmdlets.psd1
```

## Available Cmdlets

All cmdlets follow standard PowerShell naming conventions with proper parameter binding and pipeline support.

### Test-AzSdkPackage

Validates an Azure SDK package by checking for required files like README.md and CHANGELOG.md.

**Syntax:**
```powershell
Test-AzSdkPackage -PackagePath <string>
```

**Example:**
```powershell
Test-AzSdkPackage -PackagePath "./my-package"

# Pipeline support
Get-ChildItem -Directory | ForEach-Object { Test-AzSdkPackage -PackagePath $_.FullName }
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

# Pipeline support
Get-AzSdkFiles -Path "." | Where-Object { $_.Length -gt 1000 }
```

**Returns:** Array of `FileInfo` objects.

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

# Pipeline support
$packages | ForEach-Object { New-AzSdkMetadata -PackageName $_.Name -Version $_.Version }
```

**Returns:** Path to the generated metadata file.

### Show-AzSdkHelp

Displays help information for all available cmdlets.

**Syntax:**
```powershell
Show-AzSdkHelp
```

**Example:**
```powershell
Show-AzSdkHelp
```

## Getting Detailed Help

For detailed help on any cmdlet, use PowerShell's built-in help system:

```powershell
Get-Help Test-AzSdkPackage -Detailed
Get-Help Get-AzSdkFiles -Examples
Get-Help New-AzSdkMetadata -Full
```

## Running Tests

This module includes comprehensive Pester tests for all cmdlets:

```powershell
# Navigate to the Tests directory
cd ./PowerShell/Tests

# Run all tests
Invoke-Pester -Path ./AzSdkCli.Cmdlets.Tests.ps1

# Run tests with detailed output
Invoke-Pester -Path ./AzSdkCli.Cmdlets.Tests.ps1 -Output Detailed
```

## Building the Cmdlets

The cmdlets are implemented in C# in the `AzSdkCli.Cmdlets` project:

```bash
cd AzSdkCli.Cmdlets
dotnet build
```

The compiled DLL is automatically copied to the PowerShell directory for distribution.

## Advantages of Binary Cmdlets

Binary cmdlets offer several advantages over script-based functions:

1. **Performance**: Compiled code runs faster than interpreted scripts
2. **Type Safety**: Strong typing catches errors at compile time
3. **IntelliSense**: Better IDE support with proper parameter completion
4. **Pipeline Support**: Native PowerShell pipeline integration
5. **Error Handling**: Built-in PowerShell error handling mechanisms
6. **Help Integration**: XML-based help documentation from code comments

## Comparison with Script Functions

The binary cmdlets provide the same functionality as the script-based functions but with enhanced features:

| Feature | Script Functions | Binary Cmdlets |
|---------|-----------------|----------------|
| Performance | Good | Excellent |
| Type Safety | Runtime | Compile-time |
| Pipeline Support | Manual | Native |
| Parameter Validation | Attributes | Built-in |
| Error Handling | Try/Catch | PSCmdlet |
| Help Documentation | Comment-based | XML from code |

## Requirements

- PowerShell 5.1 or later
- .NET 8.0 SDK (for building)
- PowerShell Core 6.0+ recommended for cross-platform support

## Examples

### Example 1: Validate Multiple Packages

```powershell
$packages = Get-ChildItem -Directory
$results = $packages | ForEach-Object {
    [PSCustomObject]@{
        Package = $_.Name
        Valid = Test-AzSdkPackage -PackagePath $_.FullName
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

### Example 3: Find Large TypeScript Files

```powershell
$files = Get-AzSdkFiles -Path "./src" -Pattern "*.ts"
$largeFiles = $files | Where-Object { $_.Length -gt 10KB }
Write-Host "Found $($largeFiles.Count) large TypeScript files"
$largeFiles | Select-Object Name, Length | Format-Table
```

## License

See the repository LICENSE file for details.
