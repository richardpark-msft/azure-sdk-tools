using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AzSdkCli
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var command = args[0].ToLower();
            var commandArgs = args.Skip(1).ToArray();

            return command switch
            {
                "validate-package" => ValidatePackage(commandArgs),
                "list-files" => ListFiles(commandArgs),
                "generate-metadata" => GenerateMetadata(commandArgs),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => HandleUnknownCommand(command)
            };
        }

        static int ValidatePackage(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Error: Package path required");
                Console.WriteLine("Usage: azsdk-cli validate-package <package-path>");
                return 1;
            }

            var packagePath = args[0];
            if (!File.Exists(packagePath) && !Directory.Exists(packagePath))
            {
                Console.WriteLine($"Error: Package path '{packagePath}' does not exist");
                return 1;
            }

            Console.WriteLine($"Validating package: {packagePath}");
            
            // Simple validation logic
            var isValid = true;
            if (Directory.Exists(packagePath))
            {
                var files = Directory.GetFiles(packagePath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"Found {files.Length} files");
                
                // Check for required files
                var hasReadme = files.Any(f => Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase));
                var hasChangelog = files.Any(f => Path.GetFileName(f).Equals("CHANGELOG.md", StringComparison.OrdinalIgnoreCase));
                
                if (!hasReadme)
                {
                    Console.WriteLine("Warning: README.md not found");
                    isValid = false;
                }
                if (!hasChangelog)
                {
                    Console.WriteLine("Warning: CHANGELOG.md not found");
                    isValid = false;
                }
            }

            if (isValid)
            {
                Console.WriteLine("Package validation passed");
                return 0;
            }
            else
            {
                Console.WriteLine("Package validation failed");
                return 1;
            }
        }

        static int ListFiles(string[] args)
        {
            var path = args.Length > 0 ? args[0] : ".";
            var pattern = args.Length > 1 ? args[1] : "*.*";

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Error: Directory '{path}' does not exist");
                return 1;
            }

            Console.WriteLine($"Listing files in: {path}");
            Console.WriteLine($"Pattern: {pattern}");
            Console.WriteLine();

            var files = Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"{Path.GetFileName(file)} ({fileInfo.Length} bytes)");
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {files.Length} file(s)");
            return 0;
        }

        static int GenerateMetadata(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Package name and version required");
                Console.WriteLine("Usage: azsdk-cli generate-metadata <package-name> <version> [output-path]");
                return 1;
            }

            var packageName = args[0];
            var version = args[1];
            var outputPath = args.Length > 2 ? args[2] : "metadata.json";

            var metadata = new
            {
                packageName = packageName,
                version = version,
                generatedAt = DateTime.UtcNow.ToString("o"),
                tool = "azsdk-cli",
                toolVersion = "1.0.0"
            };

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);

            Console.WriteLine($"Metadata generated: {outputPath}");
            Console.WriteLine(json);
            return 0;
        }

        static int ShowHelp()
        {
            Console.WriteLine("AzSdk CLI - Azure SDK Command Line Interface");
            Console.WriteLine();
            Console.WriteLine("Usage: azsdk-cli <command> [arguments]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  validate-package <package-path>           Validate an Azure SDK package");
            Console.WriteLine("  list-files <path> [pattern]               List files in a directory");
            Console.WriteLine("  generate-metadata <name> <version> [out]  Generate package metadata");
            Console.WriteLine("  help                                      Show this help message");
            Console.WriteLine();
            return 0;
        }

        static int HandleUnknownCommand(string command)
        {
            Console.WriteLine($"Error: Unknown command '{command}'");
            Console.WriteLine("Run 'azsdk-cli help' for usage information");
            return 1;
        }
    }
}
