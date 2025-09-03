using Newtonsoft.Json;

namespace PluginLauncher.Models;

/// <summary>
/// Configuration settings for the launcher
/// </summary>
public class LauncherConfig
{
    [JsonProperty("github_owner")]
    public string GitHubOwner { get; set; } = string.Empty;

    [JsonProperty("github_repo")]
    public string GitHubRepo { get; set; } = string.Empty;

    [JsonProperty("github_token")]
    public string GitHubToken { get; set; } = string.Empty;

    [JsonProperty("plugins_directory")]
    public string PluginsDirectory { get; set; } = "plugins";

    [JsonProperty("temp_directory")]
    public string TempDirectory { get; set; } = "temp";

    [JsonProperty("auto_create_directories")]
    public bool AutoCreateDirectories { get; set; } = true;

    [JsonProperty("verify_checksums")]
    public bool VerifyChecksums { get; set; } = true;

    [JsonProperty("backup_before_update")]
    public bool BackupBeforeUpdate { get; set; } = true;

    [JsonProperty("installed_plugins")]
    public List<PluginInfo> InstalledPlugins { get; set; } = new();

    public static LauncherConfig CreateDefault()
    {
        return new LauncherConfig
        {
            PluginsDirectory = Path.Combine(Environment.CurrentDirectory, "plugins"),
            TempDirectory = Path.Combine(Environment.CurrentDirectory, "temp"),
            AutoCreateDirectories = true,
            VerifyChecksums = true,
            BackupBeforeUpdate = true,
            InstalledPlugins = new List<PluginInfo>()
        };
    }
}