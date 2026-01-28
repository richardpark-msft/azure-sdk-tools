// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for managing the preview environment (shallow clones of SDK repositories).
/// </summary>
public interface IPreviewEnvironmentService
{
    /// <summary>
    /// Gets the current preview configuration.
    /// </summary>
    PreviewConfiguration Configuration { get; }

    /// <summary>
    /// Initializes the preview environment by creating shallow clones of SDK repositories.
    /// </summary>
    /// <param name="basePath">Optional custom base path for the preview environment.</param>
    /// <param name="languages">Languages to initialize (null for all).</param>
    /// <param name="force">If true, re-clone repositories even if they exist.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of clone results per language.</returns>
    Task<Dictionary<SdkLanguage, (bool Success, string? Error, TimeSpan Duration)>> InitializeAsync(
        string? basePath = null,
        IEnumerable<SdkLanguage>? languages = null,
        bool force = false,
        IProgress<(SdkLanguage Language, string Status)>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates all SDK repositories to the latest commit.
    /// </summary>
    /// <param name="languages">Languages to update (null for all).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of update results per language.</returns>
    Task<Dictionary<SdkLanguage, (bool Success, string? OldSha, string? NewSha, string? Error)>> UpdateAsync(
        IEnumerable<SdkLanguage>? languages = null,
        IProgress<(SdkLanguage Language, string Status)>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the status of all repositories in the preview environment.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of status information per language.</returns>
    Task<Dictionary<SdkLanguage, RepositoryStatusResult>> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the path to a specific SDK repository.
    /// </summary>
    /// <param name="language">The SDK language.</param>
    /// <returns>Path to the repository, or null if not initialized.</returns>
    string? GetRepoPath(SdkLanguage language);

    /// <summary>
    /// Checks if the preview environment is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the total disk usage of the preview environment.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Human-readable disk usage string.</returns>
    Task<string> GetTotalDiskUsageAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads configuration from the specified path or default location.
    /// </summary>
    /// <param name="basePath">Optional custom base path.</param>
    void LoadConfiguration(string? basePath = null);
}

/// <summary>
/// Result of a repository status check.
/// </summary>
public class RepositoryStatusResult
{
    public bool IsCloned { get; set; }
    public string? LocalPath { get; set; }
    public string? Branch { get; set; }
    public string? CommitSha { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? DiskUsage { get; set; }
    public string? Error { get; set; }
}
