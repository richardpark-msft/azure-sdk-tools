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
/// Tool for updating the preview environment repositories to the latest commit.
/// </summary>
[McpServerToolType, Description("Update the preview environment repositories to the latest commit.")]
public class PreviewUpdateTool(
    IPreviewEnvironmentService previewService,
    ILogger<PreviewUpdateTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Preview];

    private const string UpdateCommandName = "update";
    private const string UpdateToolName = "azsdk_preview_update";

    private readonly Option<string[]> languagesOpt = new("--languages", "-l")
    {
        Description = "Comma-separated list of languages to update (dotnet, java, js, python, go). Default: all",
        Required = false,
        AllowMultipleArgumentsPerToken = true
    };

    protected override Command GetCommand()
    {
        return new McpCommand(UpdateCommandName, "Update the preview environment repositories to the latest commit", UpdateToolName)
        {
            languagesOpt
        };
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var languagesArg = parseResult.GetValue(languagesOpt);
        return await UpdatePreviewAsync(languagesArg, isCli: true, ct);
    }

    [McpServerTool(Name = UpdateToolName), Description("Update the preview environment repositories to pull the latest changes.")]
    public async Task<PreviewResponse> UpdatePreviewAsync(
        [Description("Languages to update. Options: dotnet, java, js, python, go. Leave empty to update all.")]
        string[]? languages,
        bool isCli = false,
        CancellationToken ct = default)
    {
        try
        {
            // Check if preview environment is initialized
            if (!previewService.IsInitialized)
            {
                return PreviewResponse.CreateFailure(
                    "Preview environment is not initialized. Run 'azsdk preview init' first.",
                    previewService.Configuration.BasePath);
            }

            // Parse languages if provided
            var targetLanguages = ParseLanguages(languages);
            if (targetLanguages.Count == 0)
            {
                targetLanguages = PreviewConfiguration.SdkRepositories.Keys.ToList();
            }

            if (isCli)
            {
                Console.WriteLine("Updating preview repositories...");
                Console.WriteLine();
            }

            // Track progress for CLI output
            var progress = new Progress<(SdkLanguage Language, string Status)>(update =>
            {
                if (isCli)
                {
                    Console.WriteLine($"  {GetLanguageRepoName(update.Language),-25} {update.Status}");
                }
                logger.LogInformation("{Language}: {Status}", update.Language, update.Status);
            });

            // Update repositories
            var results = await previewService.UpdateAsync(
                languages: targetLanguages,
                progress: progress,
                ct: ct);

            // Build response
            var updatedCount = 0;
            var alreadyUpToDate = 0;
            var failed = 0;

            foreach (var (lang, (success, oldSha, newSha, error)) in results)
            {
                if (!success)
                {
                    failed++;
                    if (isCli)
                    {
                        Console.WriteLine($"  ✗ {GetLanguageRepoName(lang),-25} Failed: {error}");
                    }
                }
                else if (oldSha == newSha)
                {
                    alreadyUpToDate++;
                    if (isCli)
                    {
                        Console.WriteLine($"  ✓ {GetLanguageRepoName(lang),-25} (already up to date)");
                    }
                }
                else
                {
                    updatedCount++;
                    var oldShort = oldSha?.Length > 7 ? oldSha[..7] : oldSha;
                    var newShort = newSha?.Length > 7 ? newSha[..7] : newSha;
                    if (isCli)
                    {
                        Console.WriteLine($"  ✓ {GetLanguageRepoName(lang),-25} ({oldShort} → {newShort})");
                    }
                }
            }

            if (isCli)
            {
                Console.WriteLine();
                Console.WriteLine("Preview environment updated!");
            }

            if (failed > 0)
            {
                var failures = results.Where(r => !r.Value.Success).Select(r => $"{r.Key}: {r.Value.Error}");
                return new PreviewResponse
                {
                    IsSuccessful = updatedCount + alreadyUpToDate > 0,
                    Result = $"Updated {updatedCount} repositories, {alreadyUpToDate} already up to date, {failed} failed.",
                    ResponseError = string.Join("; ", failures),
                    PreviewEnvironmentPath = previewService.Configuration.BasePath
                };
            }

            return PreviewResponse.CreateSuccess(
                $"Updated {updatedCount} repositories, {alreadyUpToDate} already up to date.",
                previewService.Configuration.BasePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating preview environment");
            return PreviewResponse.CreateFailure(
                $"Failed to update preview environment: {ex.Message}",
                previewService.Configuration.BasePath);
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

    private static string GetLanguageRepoName(SdkLanguage lang) => lang switch
    {
        SdkLanguage.DotNet => "azure-sdk-for-net",
        SdkLanguage.Java => "azure-sdk-for-java",
        SdkLanguage.JavaScript => "azure-sdk-for-js",
        SdkLanguage.Python => "azure-sdk-for-python",
        SdkLanguage.Go => "azure-sdk-for-go",
        _ => $"azure-sdk-for-{lang.ToString().ToLower()}"
    };
}
