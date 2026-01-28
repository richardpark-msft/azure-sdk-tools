// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Service for managing the preview environment (shallow clones of SDK repositories).
/// </summary>
public class PreviewEnvironmentService : IPreviewEnvironmentService
{
    private readonly IProcessHelper _processHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<PreviewEnvironmentService> _logger;
    private PreviewConfiguration _configuration;

    public PreviewEnvironmentService(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        ILogger<PreviewEnvironmentService> logger)
    {
        _processHelper = processHelper;
        _gitHelper = gitHelper;
        _logger = logger;
        _configuration = PreviewConfiguration.Load();
    }

    public PreviewConfiguration Configuration => _configuration;

    public bool IsInitialized => _configuration.IsInitialized;

    public void LoadConfiguration(string? basePath = null)
    {
        _configuration = PreviewConfiguration.Load(basePath);
    }

    public string? GetRepoPath(SdkLanguage language)
    {
        if (_configuration.Repositories.TryGetValue(language, out var repoInfo) && repoInfo.IsCloned)
        {
            return repoInfo.LocalPath;
        }

        // Check if the default path exists even if not in config
        var defaultPath = _configuration.GetRepoPath(language);
        if (Directory.Exists(defaultPath))
        {
            return defaultPath;
        }

        return null;
    }

    public async Task<Dictionary<SdkLanguage, (bool Success, string? Error, TimeSpan Duration)>> InitializeAsync(
        string? basePath = null,
        IEnumerable<SdkLanguage>? languages = null,
        bool force = false,
        IProgress<(SdkLanguage Language, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(basePath))
        {
            _configuration = new PreviewConfiguration { BasePath = basePath };
        }

        var results = new Dictionary<SdkLanguage, (bool Success, string? Error, TimeSpan Duration)>();
        var targetLanguages = languages?.ToList() ?? PreviewConfiguration.SdkRepositories.Keys.ToList();

        // Create base directories
        Directory.CreateDirectory(_configuration.ReposPath);
        Directory.CreateDirectory(_configuration.WorkspacesPath);

        // Clone repositories in parallel
        var cloneTasks = targetLanguages.Select(async lang =>
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var result = await CloneRepositoryAsync(lang, force, progress, ct);
                var duration = DateTime.UtcNow - startTime;
                return (Language: lang, Success: result.Success, Error: result.Error, Duration: duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Failed to clone repository for {Language}", lang);
                return (Language: lang, Success: false, Error: ex.Message, Duration: duration);
            }
        });

        var cloneResults = await Task.WhenAll(cloneTasks);

        foreach (var result in cloneResults)
        {
            results[result.Language] = (result.Success, result.Error, result.Duration);
        }

        // Save configuration
        _configuration.Save();

        return results;
    }

    private async Task<(bool Success, string? Error)> CloneRepositoryAsync(
        SdkLanguage language,
        bool force,
        IProgress<(SdkLanguage Language, string Status)>? progress,
        CancellationToken ct)
    {
        if (!PreviewConfiguration.SdkRepositories.TryGetValue(language, out var metadata))
        {
            return (false, $"Unknown language: {language}");
        }

        var repoPath = _configuration.GetRepoPath(language);

        // Check if already exists
        if (Directory.Exists(repoPath))
        {
            if (!force)
            {
                // Update existing repository info
                progress?.Report((language, "Already exists, skipping..."));
                await UpdateRepositoryInfoAsync(language, repoPath, ct);
                return (true, null);
            }

            // Force re-clone: delete existing
            progress?.Report((language, "Removing existing clone..."));
            try
            {
                Directory.Delete(repoPath, recursive: true);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete existing repository: {ex.Message}");
            }
        }

        progress?.Report((language, "Cloning (shallow)..."));
        _logger.LogInformation("Cloning {RepoName} to {Path}", metadata.RepoName, repoPath);

        // Perform shallow clone
        var processOptions = new ProcessOptions(
            "git",
            ["clone", "--depth", "1", "--single-branch", metadata.CloneUrl, repoPath],
            logOutputStream: true,
            timeout: TimeSpan.FromMinutes(30)
        );

        var result = await _processHelper.Run(processOptions, ct);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Git clone failed for {Language}: {Output}", language, result.Output);
            return (false, $"Git clone failed: {result.Output}");
        }

        progress?.Report((language, "Clone complete"));

        // Update configuration with repository info
        await UpdateRepositoryInfoAsync(language, repoPath, ct);

        return (true, null);
    }

    private async Task UpdateRepositoryInfoAsync(SdkLanguage language, string repoPath, CancellationToken ct)
    {
        try
        {
            var commitSha = await GetCurrentCommitShaAsync(repoPath, ct);
            var branch = await GetCurrentBranchAsync(repoPath, ct);

            _configuration.Repositories[language] = new RepositoryInfo
            {
                LocalPath = repoPath,
                CommitSha = commitSha ?? string.Empty,
                Branch = branch ?? "main",
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get repository info for {Language}", language);
            _configuration.Repositories[language] = new RepositoryInfo
            {
                LocalPath = repoPath,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task<Dictionary<SdkLanguage, (bool Success, string? OldSha, string? NewSha, string? Error)>> UpdateAsync(
        IEnumerable<SdkLanguage>? languages = null,
        IProgress<(SdkLanguage Language, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new Dictionary<SdkLanguage, (bool Success, string? OldSha, string? NewSha, string? Error)>();
        var targetLanguages = languages?.ToList() ?? _configuration.Repositories.Keys.ToList();

        var updateTasks = targetLanguages.Select(async lang =>
        {
            try
            {
                var result = await UpdateRepositoryAsync(lang, progress, ct);
                return (Language: lang, result.Success, result.OldSha, result.NewSha, result.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update repository for {Language}", lang);
                return (Language: lang, Success: false, OldSha: (string?)null, NewSha: (string?)null, Error: ex.Message);
            }
        });

        var updateResults = await Task.WhenAll(updateTasks);

        foreach (var result in updateResults)
        {
            results[result.Language] = (result.Success, result.OldSha, result.NewSha, result.Error);
        }

        _configuration.Save();

        return results;
    }

    private async Task<(bool Success, string? OldSha, string? NewSha, string? Error)> UpdateRepositoryAsync(
        SdkLanguage language,
        IProgress<(SdkLanguage Language, string Status)>? progress,
        CancellationToken ct)
    {
        var repoPath = GetRepoPath(language);
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            return (false, null, null, "Repository not initialized");
        }

        progress?.Report((language, "Fetching updates..."));

        var oldSha = await GetCurrentCommitShaAsync(repoPath, ct);

        // Fetch and reset to origin/main (or whatever the default branch is)
        var fetchOptions = new ProcessOptions(
            "git",
            ["fetch", "--depth", "1", "origin"],
            workingDirectory: repoPath,
            timeout: TimeSpan.FromMinutes(10)
        );

        var fetchResult = await _processHelper.Run(fetchOptions, ct);
        if (fetchResult.ExitCode != 0)
        {
            return (false, oldSha, null, $"Fetch failed: {fetchResult.Output}");
        }

        var resetOptions = new ProcessOptions(
            "git",
            ["reset", "--hard", "origin/HEAD"],
            workingDirectory: repoPath,
            timeout: TimeSpan.FromMinutes(5)
        );

        var resetResult = await _processHelper.Run(resetOptions, ct);
        if (resetResult.ExitCode != 0)
        {
            return (false, oldSha, null, $"Reset failed: {resetResult.Output}");
        }

        var newSha = await GetCurrentCommitShaAsync(repoPath, ct);

        // Update configuration
        if (_configuration.Repositories.TryGetValue(language, out var repoInfo))
        {
            repoInfo.CommitSha = newSha ?? string.Empty;
            repoInfo.LastUpdated = DateTime.UtcNow;
        }

        if (oldSha == newSha)
        {
            progress?.Report((language, "Already up to date"));
        }
        else
        {
            progress?.Report((language, $"Updated to {newSha?[..7]}"));
        }

        return (true, oldSha, newSha, null);
    }

    public async Task<Dictionary<SdkLanguage, RepositoryStatusResult>> GetStatusAsync(CancellationToken ct = default)
    {
        var results = new Dictionary<SdkLanguage, RepositoryStatusResult>();

        foreach (var (language, metadata) in PreviewConfiguration.SdkRepositories)
        {
            var status = new RepositoryStatusResult();
            var repoPath = _configuration.GetRepoPath(language);

            if (Directory.Exists(repoPath))
            {
                status.IsCloned = true;
                status.LocalPath = repoPath;

                try
                {
                    status.CommitSha = await GetCurrentCommitShaAsync(repoPath, ct);
                    status.Branch = await GetCurrentBranchAsync(repoPath, ct);
                    status.DiskUsage = await GetDirectorySizeAsync(repoPath, ct);

                    if (_configuration.Repositories.TryGetValue(language, out var repoInfo))
                    {
                        status.LastUpdated = repoInfo.LastUpdated;
                    }
                }
                catch (Exception ex)
                {
                    status.Error = ex.Message;
                }
            }
            else
            {
                status.IsCloned = false;
            }

            results[language] = status;
        }

        return results;
    }

    public async Task<string> GetTotalDiskUsageAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_configuration.BasePath))
        {
            return "0 B";
        }

        return await GetDirectorySizeAsync(_configuration.BasePath, ct);
    }

    private async Task<string?> GetCurrentCommitShaAsync(string repoPath, CancellationToken ct)
    {
        var options = new ProcessOptions(
            "git",
            ["rev-parse", "HEAD"],
            workingDirectory: repoPath,
            timeout: TimeSpan.FromSeconds(10)
        );

        var result = await _processHelper.Run(options, ct);
        return result.ExitCode == 0 ? result.Output?.Trim() : null;
    }

    private async Task<string?> GetCurrentBranchAsync(string repoPath, CancellationToken ct)
    {
        var options = new ProcessOptions(
            "git",
            ["rev-parse", "--abbrev-ref", "HEAD"],
            workingDirectory: repoPath,
            timeout: TimeSpan.FromSeconds(10)
        );

        var result = await _processHelper.Run(options, ct);
        return result.ExitCode == 0 ? result.Output?.Trim() : null;
    }

    private async Task<string> GetDirectorySizeAsync(string path, CancellationToken ct)
    {
        try
        {
            // Use du command on Unix-like systems, or fall back to .NET calculation
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var options = new ProcessOptions(
                    "du",
                    ["-sh", path],
                    timeout: TimeSpan.FromSeconds(30)
                );

                var result = await _processHelper.Run(options, ct);
                if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.Output))
                {
                    // du output format: "1.2G\t/path/to/dir"
                    var parts = result.Output.Split('\t');
                    if (parts.Length > 0)
                    {
                        return parts[0].Trim();
                    }
                }
            }

            // Fallback: calculate manually (slower)
            return await Task.Run(() => GetDirectorySizeManual(path), ct);
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetDirectorySizeManual(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            long size = directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return FormatBytes(size);
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }
}
