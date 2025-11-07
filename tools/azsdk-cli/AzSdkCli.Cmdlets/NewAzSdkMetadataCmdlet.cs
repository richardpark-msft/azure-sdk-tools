using System;
using System.IO;
using System.Management.Automation;
using System.Text.Json;

namespace AzSdkCli.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Generates metadata for an Azure SDK package.</para>
    /// <para type="description">Generates a JSON metadata file containing package information.</para>
    /// </summary>
    /// <example>
    ///   <code>New-AzSdkMetadata -PackageName "Azure.Core" -Version "1.0.0" -OutputPath "metadata.json"</code>
    ///   <para>Generates a metadata file for the specified package.</para>
    /// </example>
    [Cmdlet(VerbsCommon.New, "AzSdkMetadata")]
    [OutputType(typeof(string))]
    public class NewAzSdkMetadataCmdlet : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The name of the package.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string PackageName { get; set; } = null!;

        /// <summary>
        /// <para type="description">The version of the package.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; } = null!;

        /// <summary>
        /// <para type="description">The output path for the metadata file (default: metadata.json).</para>
        /// </summary>
        [Parameter(Position = 2)]
        public string OutputPath { get; set; } = "metadata.json";

        protected override void ProcessRecord()
        {
            var metadata = new
            {
                packageName = PackageName,
                version = Version,
                generatedAt = DateTime.UtcNow.ToString("o"),
                tool = "azsdk-cli",
                toolVersion = "1.0.0"
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(metadata, options);

            File.WriteAllText(OutputPath, json);

            WriteHost($"Metadata generated: {OutputPath}");
            WriteHost(json);

            WriteObject(OutputPath);
        }

        private void WriteHost(string message)
        {
            Host.UI.WriteLine(message);
        }
    }
}
