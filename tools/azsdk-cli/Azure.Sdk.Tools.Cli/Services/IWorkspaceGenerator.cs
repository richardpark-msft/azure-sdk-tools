// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for generating VS Code workspace files for SDK preview.
/// </summary>
public interface IWorkspaceGenerator
{
    /// <summary>
    /// Generates a VS Code workspace file for previewing generated SDK code.
    /// </summary>
    /// <param name="serviceName">Name of the service (used for workspace file name).</param>
    /// <param name="typeSpecProjectPath">Path to the TypeSpec project (source).</param>
    /// <param name="generatedPackages">Dictionary of language to generated package path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the workspace file path.</returns>
    Task<WorkspaceGeneratorResult> GenerateWorkspaceAsync(
        string serviceName,
        string typeSpecProjectPath,
        Dictionary<SdkLanguage, string> generatedPackages,
        CancellationToken ct = default);
}

/// <summary>
/// Result of workspace generation.
/// </summary>
public class WorkspaceGeneratorResult
{
    public bool Success { get; set; }
    public string? WorkspacePath { get; set; }
    public string? Error { get; set; }
}
