// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Preview;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Preview;

/// <summary>
/// Tool for checking the status of the preview environment.
/// </summary>
[McpServerToolType, Description("Check the status of the preview environment.")]
public class PreviewStatusTool(
    IPreviewEnvironmentService previewService,
    ILogger<PreviewStatusTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Preview];

    private const string StatusCommandName = "status";
    private const string StatusToolName = "azsdk_preview_status";

    protected override Command GetCommand()
    {
        return new McpCommand(StatusCommandName, "Check the status of the preview environment", StatusToolName);
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        return await GetStatusAsync(isCli: true, ct);
    }

    [McpServerTool(Name = StatusToolName), Description("Check the status of the preview environment, showing cloned repositories and disk usage.")]
    public async Task<PreviewStatusResponse> GetStatusAsync(
        bool isCli = false,
        CancellationToken ct = default)
    {
        try
        {
            var response = new PreviewStatusResponse
            {
                PreviewEnvironmentPath = previewService.Configuration.BasePath
            };

            // Check if environment exists
            if (!Directory.Exists(previewService.Configuration.BasePath))
            {
                response.IsSuccessful = true;
                response.Result = "Preview environment not initialized.";
                response.NextSteps = ["Run 'azsdk preview init' to set up the preview environment"];

                if (isCli)
                {
                    Console.WriteLine("Preview environment not initialized.");
                    Console.WriteLine();
                    Console.WriteLine("Run 'azsdk preview init' to set up the preview environment.");
                }

                return response;
            }

            if (isCli)
            {
                Console.WriteLine("Preview Environment Status");
                Console.WriteLine("==========================");
                Console.WriteLine();
                Console.WriteLine($"Location: {previewService.Configuration.BasePath}");
            }

            // Get status for each repository
            var status = await previewService.GetStatusAsync(ct);

            foreach (var (lang, repoStatus) in status)
            {
                response.Repositories[lang] = new RepositoryStatusInfo
                {
                    IsCloned = repoStatus.IsCloned,
                    LocalPath = repoStatus.LocalPath,
                    Branch = repoStatus.Branch,
                    CommitSha = repoStatus.CommitSha,
                    LastUpdated = repoStatus.LastUpdated,
                    DiskUsage = repoStatus.DiskUsage
                };
            }

            // Get total disk usage
            response.TotalDiskUsage = await previewService.GetTotalDiskUsageAsync(ct);

            if (isCli)
            {
                Console.WriteLine($"Disk Usage: {response.TotalDiskUsage}");
                Console.WriteLine();
                Console.WriteLine("Repositories:");

                foreach (var (lang, repoStatus) in response.Repositories)
                {
                    var statusIcon = repoStatus.IsCloned ? "✓" : "✗";
                    if (repoStatus.IsCloned)
                    {
                        var shortSha = repoStatus.CommitSha?.Length > 7 ? repoStatus.CommitSha[..7] : repoStatus.CommitSha;
                        var updatedAgo = repoStatus.LastUpdated.HasValue
                            ? GetTimeAgo(repoStatus.LastUpdated.Value)
                            : "unknown";
                        Console.WriteLine($"  {statusIcon} {GetLanguageRepoName(lang),-25} ({repoStatus.Branch} @ {shortSha}, updated {updatedAgo})");
                    }
                    else
                    {
                        Console.WriteLine($"  {statusIcon} {GetLanguageRepoName(lang),-25} (not cloned)");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Run 'azsdk preview update' to pull latest changes.");
            }

            response.IsSuccessful = true;
            response.Result = previewService.IsInitialized
                ? $"Preview environment initialized with {response.Repositories.Count(r => r.Value.IsCloned)} repositories."
                : "Preview environment not initialized.";

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting preview status");
            return new PreviewStatusResponse
            {
                IsSuccessful = false,
                ResponseError = $"Failed to get preview status: {ex.Message}"
            };
        }
    }

    private static string GetLanguageRepoName(SdkLanguage lang) => lang switch
    {
        SdkLanguage.DotNet => "azure-sdk-for-net",
        SdkLanguage.Java => "azure-sdk-for-java",
        SdkLanguage.JavaScript => "azure-sdk-for-js",
        SdkLanguage.Python => "azure-sdk-for-python",
        SdkLanguage.Go => "azure-sdk-for-go",
        _ => $"azure-sdk-for-{lang.ToString().ToLower()}"
    };

    private static string GetTimeAgo(DateTime time)
    {
        var span = DateTime.UtcNow - time;

        if (span.TotalMinutes < 1)
        {
            return "just now";
        }

        if (span.TotalMinutes < 60)
        {
            return $"{(int)span.TotalMinutes} minute(s) ago";
        }

        if (span.TotalHours < 24)
        {
            return $"{(int)span.TotalHours} hour(s) ago";
        }

        if (span.TotalDays < 7)
        {
            return $"{(int)span.TotalDays} day(s) ago";
        }

        return time.ToString("yyyy-MM-dd");
    }
}
