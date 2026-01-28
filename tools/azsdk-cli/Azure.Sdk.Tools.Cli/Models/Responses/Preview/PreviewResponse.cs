// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Preview;

/// <summary>
/// Base response for preview operations.
/// </summary>
public class PreviewResponse : CommandResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    [JsonPropertyName("is_successful")]
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Result message from the operation.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    /// <summary>
    /// Path to the preview environment base directory.
    /// </summary>
    [JsonPropertyName("preview_environment_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviewEnvironmentPath { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Result))
        {
            sb.AppendLine(Result);
        }
        if (!string.IsNullOrEmpty(PreviewEnvironmentPath))
        {
            sb.AppendLine($"Preview Environment: {PreviewEnvironmentPath}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static PreviewResponse CreateSuccess(string message, string? previewPath = null, string[]? nextSteps = null)
    {
        return new PreviewResponse
        {
            IsSuccessful = true,
            Result = message,
            PreviewEnvironmentPath = previewPath,
            NextSteps = nextSteps?.ToList()
        };
    }

    /// <summary>
    /// Creates a failure response.
    /// </summary>
    public static PreviewResponse CreateFailure(string error, string? previewPath = null)
    {
        return new PreviewResponse
        {
            IsSuccessful = false,
            ResponseError = error,
            PreviewEnvironmentPath = previewPath
        };
    }
}

/// <summary>
/// Response for preview init operation.
/// </summary>
public class PreviewInitResponse : PreviewResponse
{
    /// <summary>
    /// Status of each repository clone operation.
    /// </summary>
    [JsonPropertyName("repository_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<SdkLanguage, RepositoryCloneStatus> RepositoryStatus { get; set; } = new();

    /// <summary>
    /// Total disk space used by the preview environment.
    /// </summary>
    [JsonPropertyName("total_disk_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TotalDiskUsage { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.Append(base.Format());

        if (RepositoryStatus.Count > 0)
        {
            sb.AppendLine("Repository Status:");
            foreach (var (lang, status) in RepositoryStatus)
            {
                var statusIcon = status.Success ? "✓" : "✗";
                var info = status.Success ? status.LocalPath : status.Error;
                sb.AppendLine($"  {statusIcon} {lang}: {info}");
            }
        }

        if (!string.IsNullOrEmpty(TotalDiskUsage))
        {
            sb.AppendLine($"Total Disk Usage: {TotalDiskUsage}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Status of a single repository clone operation.
/// </summary>
public class RepositoryCloneStatus
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("local_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalPath { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonPropertyName("disk_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiskUsage { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Response for preview run operation.
/// </summary>
public class PreviewRunResponse : PreviewResponse
{
    /// <summary>
    /// Status of SDK generation for each language.
    /// </summary>
    [JsonPropertyName("generation_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<SdkLanguage, GenerationStatus> GenerationStatus { get; set; } = new();

    /// <summary>
    /// Path to the generated VS Code workspace file.
    /// </summary>
    [JsonPropertyName("workspace_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkspacePath { get; set; }

    /// <summary>
    /// Relative path to the TypeSpec project from the spec repo root.
    /// </summary>
    [JsonPropertyName("typespec_project_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeSpecProjectPath { get; set; }

    /// <summary>
    /// Whether watch mode is active.
    /// </summary>
    [JsonPropertyName("watch_mode_active")]
    public bool WatchModeActive { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.Append(base.Format());

        if (!string.IsNullOrEmpty(TypeSpecProjectPath))
        {
            sb.AppendLine($"TypeSpec Project: {TypeSpecProjectPath}");
        }

        if (GenerationStatus.Count > 0)
        {
            sb.AppendLine("Generation Status:");
            foreach (var (lang, status) in GenerationStatus)
            {
                var statusIcon = status.Success ? "✓" : "✗";
                var info = status.Success ? status.PackagePath : status.Error;
                sb.AppendLine($"  {statusIcon} {lang}: {info}");
            }
        }

        if (!string.IsNullOrEmpty(WorkspacePath))
        {
            sb.AppendLine($"Workspace: {WorkspacePath}");
        }

        if (WatchModeActive)
        {
            sb.AppendLine("Watch mode: active");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Status of SDK generation for a single language.
/// </summary>
public class GenerationStatus
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("package_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PackagePath { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Duration { get; set; }

    [JsonPropertyName("build_success")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BuildSuccess { get; set; }

    [JsonPropertyName("build_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildError { get; set; }
}

/// <summary>
/// Response for preview status operation.
/// </summary>
public class PreviewStatusResponse : PreviewResponse
{
    /// <summary>
    /// Status of each repository in the preview environment.
    /// </summary>
    [JsonPropertyName("repositories")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<SdkLanguage, RepositoryStatusInfo> Repositories { get; set; } = new();

    /// <summary>
    /// Total disk space used by the preview environment.
    /// </summary>
    [JsonPropertyName("total_disk_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TotalDiskUsage { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.Append(base.Format());

        if (Repositories.Count > 0)
        {
            sb.AppendLine("Repositories:");
            foreach (var (lang, info) in Repositories)
            {
                var statusIcon = info.IsCloned ? "✓" : "✗";
                var cloneStatus = info.IsCloned ? $"cloned ({info.Branch} @ {info.CommitSha?[..Math.Min(7, info.CommitSha?.Length ?? 0)]})" : "not cloned";
                sb.AppendLine($"  {statusIcon} {lang}: {cloneStatus}");
            }
        }

        if (!string.IsNullOrEmpty(TotalDiskUsage))
        {
            sb.AppendLine($"Total Disk Usage: {TotalDiskUsage}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Status information for a repository.
/// </summary>
public class RepositoryStatusInfo
{
    [JsonPropertyName("is_cloned")]
    public bool IsCloned { get; set; }

    [JsonPropertyName("local_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalPath { get; set; }

    [JsonPropertyName("branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Branch { get; set; }

    [JsonPropertyName("commit_sha")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommitSha { get; set; }

    [JsonPropertyName("last_updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastUpdated { get; set; }

    [JsonPropertyName("disk_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiskUsage { get; set; }
}
