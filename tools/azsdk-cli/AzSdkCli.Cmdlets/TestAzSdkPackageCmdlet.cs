using System;
using System.IO;
using System.Linq;
using System.Management.Automation;

namespace AzSdkCli.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Validates an Azure SDK package.</para>
    /// <para type="description">Validates an Azure SDK package by checking for required files like README.md and CHANGELOG.md.</para>
    /// </summary>
    /// <example>
    ///   <code>Test-AzSdkPackage -PackagePath "./my-package"</code>
    ///   <para>Validates the package in the specified directory.</para>
    /// </example>
    [Cmdlet(VerbsDiagnostic.Test, "AzSdkPackage")]
    [OutputType(typeof(bool))]
    public class TestAzSdkPackageCmdlet : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The path to the package directory or file to validate.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string PackagePath { get; set; } = null!;

        protected override void ProcessRecord()
        {
            if (!Directory.Exists(PackagePath) && !File.Exists(PackagePath))
            {
                WriteError(new ErrorRecord(
                    new FileNotFoundException($"Package path '{PackagePath}' does not exist"),
                    "PackagePathNotFound",
                    ErrorCategory.ObjectNotFound,
                    PackagePath));
                WriteObject(false);
                return;
            }

            WriteHost($"Validating package: {PackagePath}");

            bool isValid = true;

            if (Directory.Exists(PackagePath))
            {
                var files = Directory.GetFiles(PackagePath, "*.*", SearchOption.AllDirectories);
                WriteHost($"Found {files.Length} files");

                // Check for required files
                var hasReadme = files.Any(f => Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase));
                var hasChangelog = files.Any(f => Path.GetFileName(f).Equals("CHANGELOG.md", StringComparison.OrdinalIgnoreCase));

                if (!hasReadme)
                {
                    WriteWarning("README.md not found");
                    isValid = false;
                }
                if (!hasChangelog)
                {
                    WriteWarning("CHANGELOG.md not found");
                    isValid = false;
                }
            }

            if (isValid)
            {
                WriteHost("Package validation passed");
            }
            else
            {
                WriteHost("Package validation failed");
            }

            WriteObject(isValid);
        }

        private void WriteHost(string message)
        {
            Host.UI.WriteLine(message);
        }
    }
}
