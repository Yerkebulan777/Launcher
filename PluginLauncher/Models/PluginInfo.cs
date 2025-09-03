using Newtonsoft.Json;

namespace PluginLauncher.Models;

/// <summary>
/// Represents information about a plugin
/// </summary>
public class PluginInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonProperty("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("install_path")]
    public string InstallPath { get; set; } = string.Empty;

    [JsonProperty("installed_date")]
    public DateTime? InstalledDate { get; set; }

    [JsonProperty("file_size")]
    public long FileSize { get; set; }

    [JsonProperty("checksum")]
    public string Checksum { get; set; } = string.Empty;

    public bool IsInstalled => InstalledDate.HasValue && !string.IsNullOrEmpty(InstallPath);

    public override string ToString()
    {
        return $"{Name} v{Version} - {Description}";
    }
}