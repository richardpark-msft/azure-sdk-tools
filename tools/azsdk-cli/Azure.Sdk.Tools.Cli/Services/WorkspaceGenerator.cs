// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for generating VS Code workspace files for SDK preview.
/// </summary>
public class WorkspaceGenerator : IWorkspaceGenerator
{
    private readonly IPreviewEnvironmentService _previewService;
    private readonly ILogger<WorkspaceGenerator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WorkspaceGenerator(
        IPreviewEnvironmentService previewService,
        ILogger<WorkspaceGenerator> logger)
    {
        _previewService = previewService;
        _logger = logger;
    }

    public async Task<WorkspaceGeneratorResult> GenerateWorkspaceAsync(
        string serviceName,
        string typeSpecProjectPath,
        Dictionary<SdkLanguage, string> generatedPackages,
        CancellationToken ct = default)
    {
        try
        {
            var workspacesDir = _previewService.Configuration.WorkspacesPath;
            Directory.CreateDirectory(workspacesDir);

            var workspaceFileName = $"{SanitizeFileName(serviceName)}.code-workspace";
            var workspacePath = Path.Combine(workspacesDir, workspaceFileName);

            var workspace = CreateWorkspace(typeSpecProjectPath, generatedPackages);

            var json = JsonSerializer.Serialize(workspace, JsonOptions);
            await File.WriteAllTextAsync(workspacePath, json, ct);

            _logger.LogInformation("Created VS Code workspace: {WorkspacePath}", workspacePath);

            return new WorkspaceGeneratorResult
            {
                Success = true,
                WorkspacePath = workspacePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate workspace");
            return new WorkspaceGeneratorResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static VsCodeWorkspace CreateWorkspace(
        string typeSpecProjectPath,
        Dictionary<SdkLanguage, string> generatedPackages)
    {
        var folders = new List<WorkspaceFolder>();

        // Add TypeSpec source folder first
        folders.Add(new WorkspaceFolder
        {
            Name = "TypeSpec (source)",
            Path = typeSpecProjectPath
        });

        // Add generated package folders in a consistent order
        var orderedLanguages = new[] { SdkLanguage.DotNet, SdkLanguage.Java, SdkLanguage.JavaScript, SdkLanguage.Python, SdkLanguage.Go };

        foreach (var lang in orderedLanguages)
        {
            if (generatedPackages.TryGetValue(lang, out var packagePath) && !string.IsNullOrEmpty(packagePath))
            {
                folders.Add(new WorkspaceFolder
                {
                    Name = GetLanguageDisplayName(lang),
                    Path = packagePath
                });
            }
        }

        return new VsCodeWorkspace
        {
            Folders = folders,
            Settings = new WorkspaceSettings
            {
                FilesReadonlyInclude = new Dictionary<string, bool>
                {
                    // Mark generated files as read-only to discourage direct edits
                    // Users should edit TypeSpec and regenerate
                }
            }
        };
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

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrEmpty(sanitized) ? "preview" : sanitized.ToLowerInvariant();
    }
}

/// <summary>
/// VS Code workspace file format.
/// </summary>
public class VsCodeWorkspace
{
    [JsonPropertyName("folders")]
    public List<WorkspaceFolder> Folders { get; set; } = [];

    [JsonPropertyName("settings")]
    public WorkspaceSettings? Settings { get; set; }
}

/// <summary>
/// A folder entry in a VS Code workspace.
/// </summary>
public class WorkspaceFolder
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// VS Code workspace settings.
/// </summary>
public class WorkspaceSettings
{
    [JsonPropertyName("files.readonlyInclude")]
    public Dictionary<string, bool>? FilesReadonlyInclude { get; set; }
}
