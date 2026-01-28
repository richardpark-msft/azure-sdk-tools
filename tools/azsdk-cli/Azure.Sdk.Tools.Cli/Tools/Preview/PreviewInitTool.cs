// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Preview;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Preview;

/// <summary>
/// Tool for initializing the preview environment with shallow clones of Azure SDK repositories.
/// </summary>
[McpServerToolType, Description("Initialize the preview environment with shallow clones of Azure SDK repositories.")]
public class PreviewInitTool(
    IPreviewEnvironmentService previewService,
    ILogger<PreviewInitTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Preview];

    private const string InitCommandName = "init";
    private const string InitToolName = "azsdk_preview_init";

    private readonly Option<string> pathOpt = new("--path", "-p")
    {
        Description = "Override the default storage location for the preview environment",
        Required = false,
    };

    private readonly Option<bool> forceOpt = new("--force", "-f")
    {
        Description = "Re-clone repositories even if they already exist",
        Required = false,
    };

    private readonly Option<string[]> languagesOpt = new("--languages", "-l")
    {
        Description = "Comma-separated list of languages to initialize (dotnet, java, js, python, go). Default: all",
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    protected override Command GetCommand()
    {
        return new McpCommand(InitCommandName, "Initialize the preview environment with shallow clones of Azure SDK repositories", InitToolName)
        {
            pathOpt, forceOpt, languagesOpt
        };
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var path = parseResult.GetValue(pathOpt);
        var force = parseResult.GetValue(forceOpt);
        var languagesArg = parseResult.GetValue(languagesOpt);

        return await InitPreviewEnvironmentAsync(path, force, languagesArg, isCli: true, ct);
    }

    [McpServerTool(Name = InitToolName), Description("Initialize the preview environment with shallow clones of Azure SDK repositories for fast SDK generation preview.")]
    public async Task<PreviewInitResponse> InitPreviewEnvironmentAsync(
        [Description("Override the default storage location for the preview environment (~/.azsdk/preview). Optional.")]
        string? path,
        [Description("Re-clone repositories even if they already exist. Default: false.")]
        bool force,
        [Description("Languages to initialize. Options: dotnet, java, js, python, go. Pass multiple values or leave empty for all.")]
        string[]? languages,
        bool isCli = false,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Initializing preview environment at: {Path}", path ?? PreviewConfiguration.GetDefaultBasePath());

            // Parse languages if provided
            var targetLanguages = ParseLanguages(languages);
            if (targetLanguages.Count == 0)
            {
                targetLanguages = PreviewConfiguration.SdkRepositories.Keys.ToList();
            }

            var response = new PreviewInitResponse
            {
                PreviewEnvironmentPath = path ?? PreviewConfiguration.GetDefaultBasePath()
            };

            // Track progress for CLI output
            var progress = new Progress<(SdkLanguage Language, string Status)>(update =>
            {
                if (isCli)
                {
                    Console.WriteLine($"  {GetLanguageDisplayName(update.Language),-12} {update.Status}");
                }
                logger.LogInformation("{Language}: {Status}", update.Language, update.Status);
            });

            if (isCli)
            {
                Console.WriteLine($"Initializing preview environment at {response.PreviewEnvironmentPath}");
                Console.WriteLine();
                Console.WriteLine("Cloning repositories:");
            }

            // Initialize the preview environment
            var results = await previewService.InitializeAsync(
                basePath: path,
                languages: targetLanguages,
                force: force,
                progress: progress,
                ct: ct);

            // Build response
            foreach (var (lang, (success, error, duration)) in results)
            {
                response.RepositoryStatus[lang] = new RepositoryCloneStatus
                {
                    Success = success,
                    Error = error,
                    Duration = duration,
                    LocalPath = previewService.GetRepoPath(lang)
                };
            }

            // Get disk usage
            response.TotalDiskUsage = await previewService.GetTotalDiskUsageAsync(ct);

            // Determine overall success
            var successCount = response.RepositoryStatus.Count(r => r.Value.Success);
            var totalCount = response.RepositoryStatus.Count;

            if (successCount == totalCount)
            {
                response.IsSuccessful = true;
                response.Result = $"Preview environment initialized successfully with {successCount} repositories.";
                response.NextSteps = [
                    "Run 'azsdk preview run' from your TypeSpec project to generate SDK code.",
                    "Use 'azsdk preview status' to check the status of the preview environment.",
                    "Use 'azsdk preview update' to pull the latest changes."
                ];

                if (isCli)
                {
                    Console.WriteLine();
                    Console.WriteLine("Preview environment ready!");
                    Console.WriteLine($"Total disk usage: {response.TotalDiskUsage}");
                    Console.WriteLine();
                    Console.WriteLine("Run 'azsdk preview run' from your TypeSpec project to start previewing.");
                }
            }
            else if (successCount > 0)
            {
                response.IsSuccessful = true;
                response.Result = $"Preview environment partially initialized: {successCount}/{totalCount} repositories cloned.";
                var failures = response.RepositoryStatus.Where(r => !r.Value.Success).Select(r => $"{r.Key}: {r.Value.Error}");
                response.ResponseError = $"Some repositories failed to clone: {string.Join("; ", failures)}";
            }
            else
            {
                response.IsSuccessful = false;
                response.ResponseError = "Failed to initialize preview environment. No repositories were cloned.";
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing preview environment");
            return new PreviewInitResponse
            {
                IsSuccessful = false,
                ResponseError = $"Failed to initialize preview environment: {ex.Message}",
                PreviewEnvironmentPath = path ?? PreviewConfiguration.GetDefaultBasePath()
            };
        }
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
            // Handle comma-separated values within a single argument
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
