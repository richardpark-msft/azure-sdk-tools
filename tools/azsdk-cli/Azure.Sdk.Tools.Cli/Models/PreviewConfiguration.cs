// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Configuration for the preview environment, including paths to shallow-cloned SDK repositories.
/// </summary>
public class PreviewConfiguration
{
    private static readonly string DefaultBasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".azsdk",
        "preview"
    );

    private const string ConfigFileName = "config.json";

    /// <summary>
    /// Base path for the preview environment (contains repos/ and workspaces/ subdirectories).
    /// </summary>
    public string BasePath { get; set; } = DefaultBasePath;

    /// <summary>
    /// Paths to each SDK repository, keyed by language.
    /// </summary>
    public Dictionary<SdkLanguage, RepositoryInfo> Repositories { get; set; } = new();

    /// <summary>
    /// When the configuration was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version of the configuration format for future compatibility.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets the path to the repos directory.
    /// </summary>
    [JsonIgnore]
    public string ReposPath => Path.Combine(BasePath, "repos");

    /// <summary>
    /// Gets the path to the workspaces directory.
    /// </summary>
    [JsonIgnore]
    public string WorkspacesPath => Path.Combine(BasePath, "workspaces");

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    [JsonIgnore]
    public string ConfigFilePath => Path.Combine(BasePath, ConfigFileName);

    /// <summary>
    /// SDK repository information for each language.
    /// </summary>
    public static readonly Dictionary<SdkLanguage, SdkRepositoryMetadata> SdkRepositories = new()
    {
        // TODO: in the future it'll be nice to let them set up forks, becuase that's probably common as well.
        // TODO: also, branches.
        // TODO: can you make worktrees off of a shallow clone?
        { SdkLanguage.DotNet, new("azure-sdk-for-net", "https://github.com/Azure/azure-sdk-for-net.git") },
        { SdkLanguage.Java, new("azure-sdk-for-java", "https://github.com/Azure/azure-sdk-for-java.git") },
        { SdkLanguage.JavaScript, new("azure-sdk-for-js", "https://github.com/Azure/azure-sdk-for-js.git") },
        { SdkLanguage.Python, new("azure-sdk-for-python", "https://github.com/Azure/azure-sdk-for-python.git") },
        { SdkLanguage.Go, new("azure-sdk-for-go", "https://github.com/Azure/azure-sdk-for-go.git") },
    };

    /// <summary>
    /// Gets the default base path for the preview environment.
    /// </summary>
    public static string GetDefaultBasePath() => DefaultBasePath;

    /// <summary>
    /// Loads configuration from the specified base path, or returns a new configuration if none exists.
    /// </summary>
    public static PreviewConfiguration Load(string? basePath = null)
    {
        var effectiveBasePath = basePath ?? DefaultBasePath;
        var configPath = Path.Combine(effectiveBasePath, ConfigFileName);

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PreviewConfiguration>(json, JsonOptions);
                if (config != null)
                {
                    config.BasePath = effectiveBasePath;
                    return config;
                }
            }
            catch
            {
                // If loading fails, return a fresh configuration
            }
        }

        return new PreviewConfiguration { BasePath = effectiveBasePath };
    }

    /// <summary>
    /// Saves the configuration to disk.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(BasePath);
        LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>
    /// Checks if the preview environment is initialized (has at least one cloned repository).
    /// </summary>
    public bool IsInitialized => Repositories.Count > 0 && Repositories.Values.Any(r => r.IsCloned);

    /// <summary>
    /// Gets the path to a specific SDK repository.
    /// </summary>
    public string GetRepoPath(SdkLanguage language)
    {
        if (SdkRepositories.TryGetValue(language, out var metadata))
        {
            return Path.Combine(ReposPath, metadata.RepoName);
        }
        throw new ArgumentException($"Unknown SDK language: {language}");
    }

    // TODO: do we really need this? What are we serializing, and why can't we be strict about the casing?
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>
/// Information about a cloned SDK repository.
/// </summary>
public class RepositoryInfo
{
    /// <summary>
    /// Local path to the repository.
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// Current commit SHA.
    /// </summary>
    public string CommitSha { get; set; } = string.Empty;

    /// <summary>
    /// Current branch name.
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// When the repository was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Whether the repository has been cloned.
    /// </summary>
    [JsonIgnore]
    public bool IsCloned => !string.IsNullOrEmpty(LocalPath) && Directory.Exists(LocalPath);
}

/// <summary>
/// Static metadata about an SDK repository.
/// </summary>
public record SdkRepositoryMetadata(string RepoName, string CloneUrl);
