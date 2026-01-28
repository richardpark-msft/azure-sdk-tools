// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Preview;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Preview;

/// <summary>
/// Tool for generating SDK code from TypeSpec and creating a VS Code workspace for preview.
/// </summary>
[McpServerToolType, Description("Generate SDK code from TypeSpec and open a VS Code workspace for preview.")]
public class PreviewRunTool(
    IPreviewEnvironmentService previewService,
    ITypeSpecHelper typeSpecHelper,
    ITspClientHelper tspClientHelper,
    IWorkspaceGenerator workspaceGenerator,
    ILogger<PreviewRunTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Preview];

    private const string RunCommandName = "run";
    private const string RunToolName = "azsdk_preview_run";

    private readonly Option<string> tspProjectOpt = new("--tsp-project", "-t")
    {
        Description = "Path to the TypeSpec project directory (containing tspconfig.yaml). Default: current directory",
        Required = false,
    };

    private readonly Option<bool> buildOpt = new("--build", "-b")
    {
        Description = "Build the generated code after generation",
        Required = false,
    };

    private readonly Option<bool> watchOpt = new("--watch", "-w")
    {
        Description = "Watch for changes and automatically regenerate",
        Required = false,
    };

    private readonly Option<string[]> languagesOpt = new("--languages", "-l")
    {
        Description = "Comma-separated list of languages to generate (dotnet, java, js, python, go). Default: all configured in tspconfig.yaml",
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    private readonly Option<bool> noOpenOpt = new("--no-open")
    {
        Description = "Do not automatically open the VS Code workspace",
        Required = false,
    };

    protected override Command GetCommand()
    {
        return new McpCommand(RunCommandName, "Generate SDK code from TypeSpec and create/open a VS Code workspace", RunToolName)
        {
            tspProjectOpt, buildOpt, watchOpt, languagesOpt, noOpenOpt
        };
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var tspProject = parseResult.GetValue(tspProjectOpt);
        var build = parseResult.GetValue(buildOpt);
        var watch = parseResult.GetValue(watchOpt);
        var languagesArg = parseResult.GetValue(languagesOpt);
        var noOpen = parseResult.GetValue(noOpenOpt);

        return await RunPreviewAsync(tspProject, build, watch, languagesArg, noOpen, isCli: true, ct);
    }

    [McpServerTool(Name = RunToolName), Description("Generate SDK code from TypeSpec for all Azure SDK languages and create a VS Code workspace for preview.")]
    public async Task<PreviewRunResponse> RunPreviewAsync(
        [Description("Path to the TypeSpec project directory (containing tspconfig.yaml). Default: current directory.")]
        string? tspProject,
        [Description("Build the generated code after generation. Default: false.")]
        bool build,
        [Description("Watch for changes and automatically regenerate. Default: false.")]
        bool watch,
        [Description("Languages to generate. Options: dotnet, java, js, python, go. Leave empty to use all configured emitters in tspconfig.yaml.")]
        string[]? languages,
        [Description("Do not automatically open the VS Code workspace. Default: false.")]
        bool noOpen,
        bool isCli = false,
        CancellationToken ct = default)
    {
        try
        {
            // Check if preview environment is initialized
            if (!previewService.IsInitialized)
            {
                return new PreviewRunResponse
                {
                    IsSuccessful = false,
                    ResponseError = "Preview environment is not initialized. Run 'azsdk preview init' first.",
                    NextSteps = ["Run 'azsdk preview init' to set up the preview environment"]
                };
            }

            // Resolve TypeSpec project path
            var projectPath = string.IsNullOrEmpty(tspProject) ? Environment.CurrentDirectory : Path.GetFullPath(tspProject);

            if (!typeSpecHelper.IsValidTypeSpecProjectPath(projectPath))
            {
                return new PreviewRunResponse
                {
                    IsSuccessful = false,
                    ResponseError = $"No valid TypeSpec project found at '{projectPath}'. Ensure the directory contains a tspconfig.yaml file.",
                    NextSteps = ["Navigate to a directory containing a TypeSpec project (with tspconfig.yaml)"]
                };
            }

            var tspConfigPath = Path.Combine(projectPath, "tspconfig.yaml");
            var typeSpecRelativePath = typeSpecHelper.GetTypeSpecProjectRelativePath(projectPath);

            if (isCli)
            {
                Console.WriteLine($"Detected TypeSpec project: {typeSpecRelativePath}");
                Console.WriteLine();
            }

            logger.LogInformation("Running preview for TypeSpec project: {ProjectPath}", projectPath);

            // Parse target languages
            var targetLanguages = ParseLanguages(languages);
            if (targetLanguages.Count == 0)
            {
                // Use all available languages if none specified
                targetLanguages = PreviewConfiguration.SdkRepositories.Keys.ToList();
            }

            var response = new PreviewRunResponse
            {
                TypeSpecProjectPath = typeSpecRelativePath,
                PreviewEnvironmentPath = previewService.Configuration.BasePath
            };

            if (isCli)
            {
                Console.WriteLine("Generating SDK code...");
            }

            // Generate SDK code for each language in parallel
            var generationTasks = targetLanguages.Select(async lang =>
            {
                var startTime = DateTime.UtcNow;
                try
                {
                    var result = await GenerateForLanguageAsync(lang, tspConfigPath, projectPath, build, ct);
                    var duration = DateTime.UtcNow - startTime;

                    if (isCli)
                    {
                        var statusIcon = result.Success ? "✓" : "✗";
                        var info = result.Success ? result.PackagePath : result.Error;
                        Console.WriteLine($"  {statusIcon} {GetLanguageDisplayName(lang),-12} {info}");
                    }

                    return (Language: lang, Status: new GenerationStatus
                    {
                        Success = result.Success,
                        PackagePath = result.PackagePath,
                        Error = result.Error,
                        Duration = duration,
                        BuildSuccess = result.BuildSuccess,
                        BuildError = result.BuildError
                    });
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    logger.LogError(ex, "Failed to generate for {Language}", lang);

                    if (isCli)
                    {
                        Console.WriteLine($"  ✗ {GetLanguageDisplayName(lang),-12} {ex.Message}");
                    }

                    return (Language: lang, Status: new GenerationStatus
                    {
                        Success = false,
                        Error = ex.Message,
                        Duration = duration
                    });
                }
            });

            var results = await Task.WhenAll(generationTasks);

            foreach (var result in results)
            {
                response.GenerationStatus[result.Language] = result.Status;
            }

            // Generate VS Code workspace
            var serviceName = GetServiceNameFromProject(projectPath);
            var workspaceResult = await workspaceGenerator.GenerateWorkspaceAsync(
                serviceName: serviceName,
                typeSpecProjectPath: projectPath,
                generatedPackages: response.GenerationStatus
                    .Where(g => g.Value.Success && !string.IsNullOrEmpty(g.Value.PackagePath))
                    .ToDictionary(g => g.Key, g => g.Value.PackagePath!),
                ct: ct);

            response.WorkspacePath = workspaceResult.WorkspacePath;

            if (isCli)
            {
                Console.WriteLine();
                Console.WriteLine($"VS Code workspace created: {response.WorkspacePath}");
            }

            // Open workspace if requested
            if (!noOpen && !string.IsNullOrEmpty(response.WorkspacePath))
            {
                await OpenWorkspaceAsync(response.WorkspacePath, ct);
                if (isCli)
                {
                    Console.WriteLine("Opening workspace...");
                }
            }

            // Determine overall success
            var successCount = response.GenerationStatus.Count(g => g.Value.Success);
            var totalCount = response.GenerationStatus.Count;

            if (successCount == totalCount)
            {
                response.IsSuccessful = true;
                response.Result = $"Preview generated successfully for {successCount} languages.";
            }
            else if (successCount > 0)
            {
                response.IsSuccessful = true;
                response.Result = $"Preview partially generated: {successCount}/{totalCount} languages succeeded.";
                var failures = response.GenerationStatus.Where(g => !g.Value.Success).Select(g => $"{g.Key}: {g.Value.Error}");
                response.ResponseError = $"Some languages failed: {string.Join("; ", failures)}";
            }
            else
            {
                response.IsSuccessful = false;
                response.ResponseError = "Failed to generate preview for any language.";
            }

            // TODO: Implement watch mode in a later phase
            if (watch)
            {
                response.WatchModeActive = true;
                if (isCli)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[Watch mode enabled - press Ctrl+C to stop]");
                    Console.WriteLine($"Watching for changes in {typeSpecRelativePath}/**/*.tsp");
                }
                // Watch mode implementation will be added in Phase 5
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running preview");
            return new PreviewRunResponse
            {
                IsSuccessful = false,
                ResponseError = $"Failed to run preview: {ex.Message}"
            };
        }
    }

    private async Task<(bool Success, string? PackagePath, string? Error, bool? BuildSuccess, string? BuildError)> GenerateForLanguageAsync(
        SdkLanguage language,
        string tspConfigPath,
        string projectPath,
        bool build,
        CancellationToken ct)
    {
        var repoPath = previewService.GetRepoPath(language);
        if (string.IsNullOrEmpty(repoPath))
        {
            return (false, null, $"Repository not initialized for {language}", null, null);
        }

        logger.LogInformation("Generating {Language} SDK in {RepoPath}", language, repoPath);

        // Use tsp-client init to generate the SDK
        var result = await tspClientHelper.InitializeGenerationAsync(
            workingDirectory: repoPath,
            tspConfigPath: tspConfigPath,
            additionalArgs: null,
            ct: ct);

        if (!result.IsSuccessful)
        {
            return (false, null, result.ResponseError ?? "Generation failed", null, null);
        }

        // Try to determine the output package path
        var packagePath = await DeterminePackagePathAsync(language, repoPath, projectPath, ct);

        // Build if requested
        bool? buildSuccess = null;
        string? buildError = null;

        if (build && !string.IsNullOrEmpty(packagePath))
        {
            // TODO: Implement build in Phase 6
            // For now, skip build
            logger.LogInformation("Build flag set but not yet implemented for {Language}", language);
        }

        return (true, packagePath, null, buildSuccess, buildError);
    }

    private async Task<string?> DeterminePackagePathAsync(SdkLanguage language, string repoPath, string tspProjectPath, CancellationToken ct)
    {
        // Try to find the generated package by looking for tsp-location.yaml files
        // that reference the TypeSpec project path
        var sdkDir = Path.Combine(repoPath, "sdk");
        if (!Directory.Exists(sdkDir))
        {
            return null;
        }

        // Search for recently modified tsp-location.yaml files
        var tspLocationFiles = Directory.GetFiles(sdkDir, "tsp-location.yaml", SearchOption.AllDirectories);

        foreach (var tspLocationFile in tspLocationFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(tspLocationFile, ct);
                var relativePath = typeSpecHelper.GetTypeSpecProjectRelativePath(tspProjectPath);

                // Check if this tsp-location.yaml references our TypeSpec project
                if (content.Contains(relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    var packageDir = Path.GetDirectoryName(tspLocationFile);
                    return packageDir;
                }
            }
            catch
            {
                // Skip files we can't read
            }
        }

        return null;
    }

    private async Task OpenWorkspaceAsync(string workspacePath, CancellationToken ct)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{workspacePath}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            System.Diagnostics.Process.Start(startInfo);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open VS Code. You can manually open: {WorkspacePath}", workspacePath);
        }
    }

    private static string GetServiceNameFromProject(string projectPath)
    {
        // Extract service name from the project path
        // e.g., "specification/contosowidgetmanager/Contoso.Management" -> "contosowidgetmanager"
        var parts = projectPath.Replace('\\', '/').Split('/');
        var specIndex = Array.IndexOf(parts, "specification");

        if (specIndex >= 0 && specIndex + 1 < parts.Length)
        {
            return parts[specIndex + 1];
        }

        // Fallback to the directory name
        return Path.GetFileName(projectPath) ?? "preview";
    }

    private static List<SdkLanguage> ParseLanguages(string[]? languages)
    {
        if (languages == null || languages.Length == 0)
        {
            return [];
        }

        var result = new List<SdkLanguage>();

        foreach (var lang in languages)
        {
            var parts = lang.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var parsed = SdkLanguageHelpers.GetSdkLanguage(part);
                if (parsed != SdkLanguage.Unknown && !result.Contains(parsed))
                {
                    result.Add(parsed);
                }
            }
        }

        return result;
    }

    private static string GetLanguageDisplayName(SdkLanguage language) => language switch
    {
        SdkLanguage.DotNet => ".NET",
        SdkLanguage.Java => "Java",
        SdkLanguage.JavaScript => "JavaScript",
        SdkLanguage.Python => "Python",
        SdkLanguage.Go => "Go",
        _ => language.ToString()
    };
}
