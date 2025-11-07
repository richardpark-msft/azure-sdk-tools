using System.Management.Automation;

namespace AzSdkCli.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Shows help information for AzSdk CLI cmdlets.</para>
    /// <para type="description">Displays help information for all available AzSdk CLI PowerShell cmdlets.</para>
    /// </summary>
    /// <example>
    ///   <code>Show-AzSdkHelp</code>
    ///   <para>Displays help information.</para>
    /// </example>
    [Cmdlet(VerbsCommon.Show, "AzSdkHelp")]
    public class ShowAzSdkHelpCmdlet : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteHost("AzSdk CLI - Azure SDK Command Line Interface (PowerShell Cmdlets)");
            WriteHost("");
            WriteHost("Available Cmdlets:");
            WriteHost("  Test-AzSdkPackage <package-path>              Validate an Azure SDK package");
            WriteHost("  Get-AzSdkFiles <path> [pattern]               List files in a directory");
            WriteHost("  New-AzSdkMetadata <name> <version> [out]      Generate package metadata");
            WriteHost("  Show-AzSdkHelp                                Show this help message");
            WriteHost("");
            WriteHost("For detailed help on any cmdlet, use: Get-Help <cmdlet-name> -Detailed");
            WriteHost("");
        }

        private void WriteHost(string message)
        {
            Host.UI.WriteLine(message);
        }
    }
}
