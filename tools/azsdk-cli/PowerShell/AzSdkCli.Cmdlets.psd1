@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'AzSdkCli.Cmdlets.dll'

    # Version number of this module.
    ModuleVersion = '2.0.0'

    # ID used to uniquely identify this module
    GUID = '12345678-1234-1234-1234-123456789012'

    # Author of this module
    Author = 'Azure SDK Team'

    # Company or vendor of this module
    CompanyName = 'Microsoft'

    # Copyright statement for this module
    Copyright = '(c) Microsoft Corporation. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'Azure SDK CLI PowerShell Cmdlets - Binary module providing utilities for Azure SDK package operations'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Cmdlets to export from this module
    CmdletsToExport = @(
        'Test-AzSdkPackage',
        'Get-AzSdkFiles',
        'New-AzSdkMetadata',
        'Show-AzSdkHelp'
    )

    # Functions to export from this module
    FunctionsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess
    PrivateData = @{
        PSData = @{
            # Tags applied to this module to aid discoverability
            Tags = @('Azure', 'SDK', 'CLI', 'Utilities', 'Cmdlets')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/Azure/azure-sdk-tools/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/Azure/azure-sdk-tools'
        }
    }
}
