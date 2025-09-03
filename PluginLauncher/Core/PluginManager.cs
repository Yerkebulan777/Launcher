using PluginLauncher.Models;
using PluginLauncher.Services;
using System.IO.Compression;

namespace PluginLauncher.Core;

/// <summary>
/// Main plugin management service
/// </summary>
public class PluginManager : IDisposable
{
    private readonly GitHubService _gitHubService;
    private readonly ConfigurationService _configService;
    private readonly LauncherConfig _config;

    public PluginManager(LauncherConfig config)
    {
        _config = config;
        _configService = new ConfigurationService();
        _gitHubService = new GitHubService(config.GitHubOwner, config.GitHubRepo, config.GitHubToken);
    }

    /// <summary>
    /// List available plugins from GitHub releases
    /// </summary>
    public async Task<OperationResult<List<PluginInfo>>> ListAvailablePluginsAsync()
    {
        try
        {
            var releasesResult = await _gitHubService.GetReleasesAsync();
            if (!releasesResult.Success)
            {
                return OperationResult<List<PluginInfo>>.CreateFailure(releasesResult.Message);
            }

            var plugins = new List<PluginInfo>();
            foreach (var release in releasesResult.Data!)
            {
                foreach (var asset in release.Assets)
                {
                    // Only include executable files or zip files
                    if (IsValidPluginFile(asset.Name))
                    {
                        var plugin = new PluginInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(asset.Name),
                            Version = release.TagName,
                            Description = release.Body,
                            Author = _config.GitHubOwner,
                            DownloadUrl = asset.DownloadUrl,
                            FileName = asset.Name,
                            FileSize = asset.Size
                        };
                        plugins.Add(plugin);
                    }
                }
            }

            return OperationResult<List<PluginInfo>>.CreateSuccess(plugins,
                $"Found {plugins.Count} available plugins");
        }
        catch (Exception ex)
        {
            return OperationResult<List<PluginInfo>>.CreateFailure(
                $"Error listing available plugins: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Install a plugin
    /// </summary>
    public async Task<OperationResult> InstallPluginAsync(PluginInfo plugin, IProgress<double>? progress = null)
    {
        try
        {
            // Check if already installed
            var installedPlugin = _configService.GetInstalledPlugin(plugin.Name);
            if (installedPlugin != null)
            {
                return OperationResult.CreateFailure($"Plugin '{plugin.Name}' is already installed. Use update command to update it.");
            }

            progress?.Report(10);

            // Create temp directory if needed
            if (!Directory.Exists(_config.TempDirectory))
            {
                Directory.CreateDirectory(_config.TempDirectory);
            }

            // Download plugin
            var tempFilePath = Path.Combine(_config.TempDirectory, plugin.FileName);
            var downloadResult = await _gitHubService.DownloadFileAsync(plugin.DownloadUrl, tempFilePath, 
                new Progress<double>(p => progress?.Report(10 + p * 0.6))); // 10% to 70%

            if (!downloadResult.Success)
            {
                return downloadResult;
            }

            progress?.Report(75);

            // Install plugin
            var installResult = await InstallPluginFileAsync(plugin, tempFilePath);
            if (!installResult.Success)
            {
                // Cleanup temp file
                try { File.Delete(tempFilePath); } catch { }
                return installResult;
            }

            progress?.Report(90);

            // Update configuration
            plugin.InstalledDate = DateTime.Now;
            var configResult = await _configService.AddInstalledPluginAsync(plugin);
            if (!configResult.Success)
            {
                return OperationResult.CreateFailure($"Plugin installed but failed to update config: {configResult.Message}");
            }

            // Cleanup temp file
            try { File.Delete(tempFilePath); } catch { }

            progress?.Report(100);

            return OperationResult.CreateSuccess($"Plugin '{plugin.Name}' v{plugin.Version} installed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error installing plugin: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Update an installed plugin
    /// </summary>
    public async Task<OperationResult> UpdatePluginAsync(string pluginName, IProgress<double>? progress = null)
    {
        try
        {
            // Check if plugin is installed
            var installedPlugin = _configService.GetInstalledPlugin(pluginName);
            if (installedPlugin == null)
            {
                return OperationResult.CreateFailure($"Plugin '{pluginName}' is not installed");
            }

            progress?.Report(5);

            // Get available plugins
            var availableResult = await ListAvailablePluginsAsync();
            if (!availableResult.Success)
            {
                return OperationResult.CreateFailure($"Failed to get available plugins: {availableResult.Message}");
            }

            progress?.Report(15);

            // Find latest version
            var latestPlugin = availableResult.Data!
                .Where(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Version)
                .FirstOrDefault();

            if (latestPlugin == null)
            {
                return OperationResult.CreateFailure($"Plugin '{pluginName}' not found in repository");
            }

            // Check if update is needed
            if (latestPlugin.Version == installedPlugin.Version)
            {
                return OperationResult.CreateSuccess($"Plugin '{pluginName}' is already up to date (v{installedPlugin.Version})");
            }

            progress?.Report(20);

            // Backup if configured
            if (_config.BackupBeforeUpdate)
            {
                var backupResult = BackupPlugin(installedPlugin);
                if (!backupResult.Success)
                {
                    Console.WriteLine($"Warning: {backupResult.Message}");
                }
            }

            progress?.Report(25);

            // Remove old version
            var removeResult = await RemovePluginAsync(pluginName, false); // Don't update config yet
            if (!removeResult.Success)
            {
                return OperationResult.CreateFailure($"Failed to remove old version: {removeResult.Message}");
            }

            progress?.Report(35);

            // Install new version
            var installProgress = new Progress<double>(p => progress?.Report(35 + p * 0.6)); // 35% to 95%
            var installResult = await InstallPluginAsync(latestPlugin, installProgress);
            
            progress?.Report(100);

            if (installResult.Success)
            {
                return OperationResult.CreateSuccess($"Plugin '{pluginName}' updated from v{installedPlugin.Version} to v{latestPlugin.Version}");
            }

            return installResult;
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error updating plugin: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Remove an installed plugin
    /// </summary>
    public async Task<OperationResult> RemovePluginAsync(string pluginName, bool updateConfig = true)
    {
        try
        {
            var installedPlugin = _configService.GetInstalledPlugin(pluginName);
            if (installedPlugin == null)
            {
                return OperationResult.CreateFailure($"Plugin '{pluginName}' is not installed");
            }

            // Remove plugin files
            if (!string.IsNullOrEmpty(installedPlugin.InstallPath) && Directory.Exists(installedPlugin.InstallPath))
            {
                Directory.Delete(installedPlugin.InstallPath, true);
            }

            // Update configuration
            if (updateConfig)
            {
                var configResult = await _configService.RemoveInstalledPluginAsync(pluginName);
                if (!configResult.Success)
                {
                    return OperationResult.CreateFailure($"Plugin files removed but failed to update config: {configResult.Message}");
                }
            }

            return OperationResult.CreateSuccess($"Plugin '{pluginName}' removed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error removing plugin: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// List installed plugins
    /// </summary>
    public List<PluginInfo> ListInstalledPlugins()
    {
        return _configService.GetConfig()?.InstalledPlugins ?? new List<PluginInfo>();
    }

    /// <summary>
    /// Install plugin file to destination
    /// </summary>
    private async Task<OperationResult> InstallPluginFileAsync(PluginInfo plugin, string sourceFilePath)
    {
        try
        {
            var pluginDir = Path.Combine(_config.PluginsDirectory, plugin.Name);
            
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            // If it's a zip file, extract it
            if (Path.GetExtension(plugin.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(sourceFilePath, pluginDir, true);
            }
            else
            {
                // Copy the file directly
                var destPath = Path.Combine(pluginDir, plugin.FileName);
                File.Copy(sourceFilePath, destPath, true);
            }

            plugin.InstallPath = pluginDir;
            
            // Calculate checksum if verification is enabled
            if (_config.VerifyChecksums)
            {
                plugin.Checksum = await GitHubService.CalculateFileChecksumAsync(sourceFilePath);
            }

            return OperationResult.CreateSuccess("Plugin files installed successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error installing plugin files: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Backup a plugin before update
    /// </summary>
    private OperationResult BackupPlugin(PluginInfo plugin)
    {
        try
        {
            if (string.IsNullOrEmpty(plugin.InstallPath) || !Directory.Exists(plugin.InstallPath))
            {
                return OperationResult.CreateFailure("Plugin install path not found");
            }

            var backupDir = Path.Combine(_config.TempDirectory, "backups", $"{plugin.Name}_v{plugin.Version}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupDir);

            // Copy all files
            CopyDirectory(plugin.InstallPath, backupDir);

            return OperationResult.CreateSuccess($"Plugin backed up to {backupDir}");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error backing up plugin: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Check if file is a valid plugin file
    /// </summary>
    private static bool IsValidPluginFile(string fileName)
    {
        var validExtensions = new[] { ".exe", ".dll", ".zip", ".msi" };
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return validExtensions.Contains(extension);
    }

    /// <summary>
    /// Copy directory recursively
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public void Dispose()
    {
        _gitHubService?.Dispose();
    }
}