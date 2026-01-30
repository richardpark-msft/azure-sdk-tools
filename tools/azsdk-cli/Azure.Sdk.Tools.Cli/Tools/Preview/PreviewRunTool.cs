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
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Tools.Preview;

// TODO: probably rename this to 'generate' or something like that.
// TODO: at a higher level, I think we should be able to give an overall name to the preview we're generating so you can 
//       have multiple lines of dev, at once, and just "resume" whenver you feel like it. We could also write a "context" 
//       file so an agent can pick it up later.

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
                    //.Where(g => g.Value.Success && !string.IsNullOrEmpty(g.Value.PackagePath))
                    .Where(g => !string.IsNullOrEmpty(g.Value.PackagePath))     // TODO: even if it doesn't succeed, it should still be in the workspace.
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

            // Handle watch mode
            if (watch && isCli)
            {
                response.WatchModeActive = true;
                Console.WriteLine();
                Console.WriteLine($"[Watch mode enabled - press Ctrl+C to stop]");
                Console.WriteLine($"Watching for changes in {typeSpecRelativePath}/**/*.tsp and tspconfig.yaml");
                Console.WriteLine();

                await RunWatchModeAsync(
                    projectPath,
                    tspConfigPath,
                    targetLanguages,
                    build,
                    response,
                    ct);
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

        // Try to determine the output package path
        var packagePath = await DeterminePackagePathAsync(language, repoPath, projectPath, ct);

        // Write the build log to the package folder if we have one
        if (!string.IsNullOrEmpty(packagePath) && !string.IsNullOrEmpty(result.CommandOutput))
        {
            try
            {
                var logPath = Path.Combine(packagePath, "typespec-emitter-build.log");
                await File.WriteAllTextAsync(logPath, result.CommandOutput, ct);
                logger.LogDebug("Wrote TypeSpec emitter build log to {LogPath}", logPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write TypeSpec emitter build log for {Language}", language);
            }
        }

        if (!result.IsSuccessful)
        {
            return (false, packagePath, result.ResponseError ?? "Generation failed", null, null);
        }

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

    /// <summary>
    /// Runs the file watcher in watch mode, regenerating SDK code when TypeSpec files change.
    /// </summary>
    private async Task RunWatchModeAsync(
        string projectPath,
        string tspConfigPath,
        List<SdkLanguage> targetLanguages,
        bool build,
        PreviewRunResponse response,
        CancellationToken ct)
    {
        const int DebounceDelayMs = 1500; // Wait 1.5 seconds after last change before regenerating

        using var watcher = new FileSystemWatcher(projectPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        var debounceTimer = new System.Timers.Timer(DebounceDelayMs) { AutoReset = false };
        var changedFiles = new ConcurrentBag<string>();
        var isRegenerating = false;
        var regenerationLock = new object();

        debounceTimer.Elapsed += async (sender, e) =>
        {
            // Collect changed files and clear the bag
            var files = new List<string>();
            while (changedFiles.TryTake(out var file))
            {
                files.Add(file);
            }

            if (files.Count == 0)
            {
                return;
            }

            lock (regenerationLock)
            {
                if (isRegenerating)
                {
                    return;
                }
                isRegenerating = true;
            }

            try
            {
                var distinctFiles = files.Distinct().ToList();
                var timestamp = DateTime.Now.ToString("HH:mm:ss");

                Console.WriteLine();
                Console.WriteLine($"[{timestamp}] Detected {distinctFiles.Count} file change(s):");
                foreach (var file in distinctFiles.Take(5))
                {
                    var relativePath = Path.GetRelativePath(projectPath, file);
                    Console.WriteLine($"  • {relativePath}");
                }
                if (distinctFiles.Count > 5)
                {
                    Console.WriteLine($"  ... and {distinctFiles.Count - 5} more");
                }

                Console.WriteLine();
                Console.WriteLine($"[{timestamp}] Regenerating SDK code...");

                await RegenerateAsync(tspConfigPath, projectPath, targetLanguages, build, response, ct);

                timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] Regeneration complete. Watching for changes...");
            }
            catch (Exception ex)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] Error during regeneration: {ex.Message}");
                logger.LogError(ex, "Error during watch mode regeneration");
            }
            finally
            {
                lock (regenerationLock)
                {
                    isRegenerating = false;
                }
            }
        };

        void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Only watch .tsp files and tspconfig.yaml
            if (!IsWatchedFile(e.FullPath))
            {
                return;
            }

            changedFiles.Add(e.FullPath);

            // Reset the debounce timer
            debounceTimer.Stop();
            debounceTimer.Start();
        }

        void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsWatchedFile(e.FullPath) && !IsWatchedFile(e.OldFullPath))
            {
                return;
            }

            changedFiles.Add(e.FullPath);

            debounceTimer.Stop();
            debounceTimer.Start();
        }

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;

        watcher.EnableRaisingEvents = true;

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected when Ctrl+C is pressed
            Console.WriteLine();
            Console.WriteLine("Watch mode stopped.");
        }
        finally
        {
            debounceTimer.Stop();
            debounceTimer.Dispose();
            watcher.EnableRaisingEvents = false;
        }
    }

    /// <summary>
    /// Determines if a file path should trigger regeneration.
    /// </summary>
    private static bool IsWatchedFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        // Watch tspconfig.yaml
        if (fileName.Equals("tspconfig.yaml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Watch .tsp files
        if (filePath.EndsWith(".tsp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Regenerates SDK code for all target languages (used in watch mode).
    /// </summary>
    private async Task RegenerateAsync(
        string tspConfigPath,
        string projectPath,
        List<SdkLanguage> targetLanguages,
        bool build,
        PreviewRunResponse response,
        CancellationToken ct)
    {
        var generationTasks = targetLanguages.Select(async lang =>
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var result = await GenerateForLanguageAsync(lang, tspConfigPath, projectPath, build, ct);
                var duration = DateTime.UtcNow - startTime;

                var statusIcon = result.Success ? "✓" : "✗";
                var info = result.Success ? $"({duration.TotalSeconds:F1}s)" : result.Error;
                Console.WriteLine($"  {statusIcon} {GetLanguageDisplayName(lang),-12} {info}");

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

                Console.WriteLine($"  ✗ {GetLanguageDisplayName(lang),-12} {ex.Message}");

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
    }
}
