using Newtonsoft.Json;
using PluginLauncher.Models;

namespace PluginLauncher.Services;

/// <summary>
/// Service for managing launcher configuration
/// </summary>
public class ConfigurationService
{
    private const string ConfigFileName = "launcher-config.json";
    private readonly string _configPath;
    private LauncherConfig? _config;

    public ConfigurationService(string? configDirectory = null)
    {
        var configDir = configDirectory ?? Environment.CurrentDirectory;
        _configPath = Path.Combine(configDir, ConfigFileName);
    }

    /// <summary>
    /// Load configuration from file or create default if not exists
    /// </summary>
    public async Task<OperationResult<LauncherConfig>> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _config = LauncherConfig.CreateDefault();
                var saveResult = await SaveConfigAsync();
                if (!saveResult.Success)
                {
                    return OperationResult<LauncherConfig>.CreateFailure(
                        $"Failed to create default config: {saveResult.Message}");
                }
                
                return OperationResult<LauncherConfig>.CreateSuccess(_config, 
                    "Created default configuration");
            }

            var json = await File.ReadAllTextAsync(_configPath);
            _config = JsonConvert.DeserializeObject<LauncherConfig>(json) ?? LauncherConfig.CreateDefault();
            
            // Ensure directories exist if auto-create is enabled
            if (_config.AutoCreateDirectories)
            {
                CreateDirectoriesIfNeeded();
            }

            return OperationResult<LauncherConfig>.CreateSuccess(_config, 
                "Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            return OperationResult<LauncherConfig>.CreateFailure(
                $"Error loading configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Save current configuration to file
    /// </summary>
    public async Task<OperationResult> SaveConfigAsync()
    {
        try
        {
            if (_config == null)
            {
                return OperationResult.CreateFailure("No configuration loaded to save");
            }

            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            await File.WriteAllTextAsync(_configPath, json);
            
            return OperationResult.CreateSuccess("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error saving configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get current configuration
    /// </summary>
    public LauncherConfig? GetConfig() => _config;

    /// <summary>
    /// Update configuration and save
    /// </summary>
    public async Task<OperationResult> UpdateConfigAsync(Action<LauncherConfig> updateAction)
    {
        try
        {
            if (_config == null)
            {
                var loadResult = await LoadConfigAsync();
                if (!loadResult.Success)
                {
                    return OperationResult.CreateFailure($"Failed to load config: {loadResult.Message}");
                }
                _config = loadResult.Data;
            }

            updateAction(_config!);
            return await SaveConfigAsync();
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error updating configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Create necessary directories
    /// </summary>
    private void CreateDirectoriesIfNeeded()
    {
        if (_config == null) return;

        try
        {
            if (!string.IsNullOrEmpty(_config.PluginsDirectory) && !Directory.Exists(_config.PluginsDirectory))
            {
                Directory.CreateDirectory(_config.PluginsDirectory);
            }

            if (!string.IsNullOrEmpty(_config.TempDirectory) && !Directory.Exists(_config.TempDirectory))
            {
                Directory.CreateDirectory(_config.TempDirectory);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - this is not critical
            Console.WriteLine($"Warning: Failed to create directories: {ex.Message}");
        }
    }

    /// <summary>
    /// Add or update installed plugin information
    /// </summary>
    public async Task<OperationResult> AddInstalledPluginAsync(PluginInfo plugin)
    {
        return await UpdateConfigAsync(config =>
        {
            var existing = config.InstalledPlugins.FirstOrDefault(p => p.Name == plugin.Name);
            if (existing != null)
            {
                config.InstalledPlugins.Remove(existing);
            }
            config.InstalledPlugins.Add(plugin);
        });
    }

    /// <summary>
    /// Remove installed plugin information
    /// </summary>
    public async Task<OperationResult> RemoveInstalledPluginAsync(string pluginName)
    {
        return await UpdateConfigAsync(config =>
        {
            var existing = config.InstalledPlugins.FirstOrDefault(p => p.Name == pluginName);
            if (existing != null)
            {
                config.InstalledPlugins.Remove(existing);
            }
        });
    }

    /// <summary>
    /// Get installed plugin by name
    /// </summary>
    public PluginInfo? GetInstalledPlugin(string pluginName)
    {
        return _config?.InstalledPlugins.FirstOrDefault(p => p.Name == pluginName);
    }
}