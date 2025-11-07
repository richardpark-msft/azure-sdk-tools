using System;
using System.IO;
using System.Management.Automation;

namespace AzSdkCli.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Lists files in a directory.</para>
    /// <para type="description">Lists files in a directory with optional pattern matching.</para>
    /// </summary>
    /// <example>
    ///   <code>Get-AzSdkFiles -Path "." -Pattern "*.cs"</code>
    ///   <para>Lists all C# files in the current directory.</para>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "AzSdkFiles")]
    [OutputType(typeof(FileInfo[]))]
    public class GetAzSdkFilesCmdlet : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The directory path to list files from.</para>
        /// </summary>
        [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Path { get; set; } = ".";

        /// <summary>
        /// <para type="description">Optional pattern to filter files (default: *.*).</para>
        /// </summary>
        [Parameter(Position = 1)]
        public string Pattern { get; set; } = "*.*";

        protected override void ProcessRecord()
        {
            if (!Directory.Exists(Path))
            {
                WriteError(new ErrorRecord(
                    new DirectoryNotFoundException($"Directory '{Path}' does not exist"),
                    "DirectoryNotFound",
                    ErrorCategory.ObjectNotFound,
                    Path));
                return;
            }

            WriteHost($"Listing files in: {Path}");
            WriteHost($"Pattern: {Pattern}");
            WriteHost("");

            var files = Directory.GetFiles(Path, Pattern, SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                WriteHost($"{fileInfo.Name} ({fileInfo.Length} bytes)");
                WriteObject(fileInfo);
            }

            WriteHost("");
            WriteHost($"Total: {files.Length} file(s)");
        }

        private void WriteHost(string message)
        {
            Host.UI.WriteLine(message);
        }
    }
}
