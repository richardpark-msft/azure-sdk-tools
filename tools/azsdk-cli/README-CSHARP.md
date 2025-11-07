# AzSdk CLI (C# Version)

This is a simple command-line interface tool for Azure SDK operations.

## Building

```bash
cd AzSdkCli
dotnet build
```

## Running

```bash
dotnet run -- <command> [arguments]
```

## Commands

### validate-package
Validates an Azure SDK package by checking for required files.

```bash
dotnet run -- validate-package <package-path>
```

### list-files
Lists files in a directory with optional pattern matching.

```bash
dotnet run -- list-files <path> [pattern]
```

Example:
```bash
dotnet run -- list-files . "*.cs"
```

### generate-metadata
Generates metadata JSON file for a package.

```bash
dotnet run -- generate-metadata <package-name> <version> [output-path]
```

Example:
```bash
dotnet run -- generate-metadata "Azure.Core" "1.0.0" "metadata.json"
```

### help
Shows help information.

```bash
dotnet run -- help
```

## Exit Codes

- `0`: Success
- `1`: Error
