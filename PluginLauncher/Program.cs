using PluginLauncher.Core;
using PluginLauncher.Models;
using PluginLauncher.Services;

namespace PluginLauncher;

/// <summary>
/// Main program class for the Plugin Launcher
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Plugin Launcher v1.0 - Download, install, update and remove plugins from GitHub");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args);

            return command switch
            {
                "list" => await HandleListCommand(options),
                "install" => await HandleInstallCommand(options),
                "update" => await HandleUpdateCommand(options),
                "remove" => await HandleRemoveCommand(options),
                "status" => await HandleStatusCommand(options),
                "config" => await HandleConfigCommand(options),
                "help" or "--help" or "-h" => ShowHelp(),
                _ => ShowError($"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> HandleListCommand(Dictionary<string, string> options)
    {
        var config = await LoadConfigurationAsync(options);
        if (config == null) return 1;

        using var pluginManager = new PluginManager(config);

        Console.WriteLine("Loading available plugins...");
        var result = await pluginManager.ListAvailablePluginsAsync();

        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.Message}");
            return 1;
        }

        if (result.Data?.Any() != true)
        {
            Console.WriteLine("No plugins found in the repository.");
            return 0;
        }

        Console.WriteLine($"\nAvailable plugins ({result.Data.Count}):");
        Console.WriteLine(new string('-', 80));

        foreach (var plugin in result.Data.OrderBy(p => p.Name))
        {
            Console.WriteLine($"Name: {plugin.Name}");
            Console.WriteLine($"Version: {plugin.Version}");
            Console.WriteLine($"Size: {FormatFileSize(plugin.FileSize)}");
            Console.WriteLine($"File: {plugin.FileName}");
            if (!string.IsNullOrEmpty(plugin.Description))
            {
                Console.WriteLine($"Description: {plugin.Description}");
            }
            Console.WriteLine();
        }

        return 0;
    }

    static async Task<int> HandleInstallCommand(Dictionary<string, string> options)
    {
        if (!options.ContainsKey("name"))
        {
            Console.WriteLine("Error: Plugin name is required. Usage: install --name <plugin-name> [--version <version>]");
            return 1;
        }

        var config = await LoadConfigurationAsync(options);
        if (config == null) return 1;

        using var pluginManager = new PluginManager(config);

        var name = options["name"];
        var version = options.GetValueOrDefault("version");

        Console.WriteLine($"Finding plugin '{name}'...");
        var availableResult = await pluginManager.ListAvailablePluginsAsync();

        if (!availableResult.Success)
        {
            Console.WriteLine($"Error: {availableResult.Message}");
            return 1;
        }

        var plugin = availableResult.Data!
            .Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Where(p => version == null || p.Version == version)
            .OrderByDescending(p => p.Version)
            .FirstOrDefault();

        if (plugin == null)
        {
            Console.WriteLine($"Plugin '{name}' not found" + (version != null ? $" with version '{version}'" : ""));
            return 1;
        }

        Console.WriteLine($"Installing {plugin.Name} v{plugin.Version}...");

        var progress = new Progress<double>(percentage =>
        {
            Console.Write($"\rProgress: {percentage:F1}%");
        });

        var result = await pluginManager.InstallPluginAsync(plugin, progress);
        Console.WriteLine(); // New line after progress

        if (result.Success)
        {
            Console.WriteLine($"✓ {result.Message}");
            return 0;
        }
        else
        {
            Console.WriteLine($"✗ {result.Message}");
            return 1;
        }
    }

    static async Task<int> HandleUpdateCommand(Dictionary<string, string> options)
    {
        if (!options.ContainsKey("name"))
        {
            Console.WriteLine("Error: Plugin name is required. Usage: update --name <plugin-name>");
            return 1;
        }

        var config = await LoadConfigurationAsync(options);
        if (config == null) return 1;

        using var pluginManager = new PluginManager(config);

        var name = options["name"];

        Console.WriteLine($"Updating plugin '{name}'...");

        var progress = new Progress<double>(percentage =>
        {
            Console.Write($"\rProgress: {percentage:F1}%");
        });

        var result = await pluginManager.UpdatePluginAsync(name, progress);
        Console.WriteLine(); // New line after progress

        if (result.Success)
        {
            Console.WriteLine($"✓ {result.Message}");
            return 0;
        }
        else
        {
            Console.WriteLine($"✗ {result.Message}");
            return 1;
        }
    }

    static async Task<int> HandleRemoveCommand(Dictionary<string, string> options)
    {
        if (!options.ContainsKey("name"))
        {
            Console.WriteLine("Error: Plugin name is required. Usage: remove --name <plugin-name>");
            return 1;
        }

        var config = await LoadConfigurationAsync(options);
        if (config == null) return 1;

        using var pluginManager = new PluginManager(config);

        var name = options["name"];

        Console.WriteLine($"Removing plugin '{name}'...");
        var result = await pluginManager.RemovePluginAsync(name);

        if (result.Success)
        {
            Console.WriteLine($"✓ {result.Message}");
            return 0;
        }
        else
        {
            Console.WriteLine($"✗ {result.Message}");
            return 1;
        }
    }

    static async Task<int> HandleStatusCommand(Dictionary<string, string> options)
    {
        var config = await LoadConfigurationAsync(options);
        if (config == null) return 1;

        using var pluginManager = new PluginManager(config);

        var installedPlugins = pluginManager.ListInstalledPlugins();

        if (!installedPlugins.Any())
        {
            Console.WriteLine("No plugins are currently installed.");
            return 0;
        }

        Console.WriteLine($"Installed plugins ({installedPlugins.Count}):");
        Console.WriteLine(new string('-', 80));

        foreach (var plugin in installedPlugins.OrderBy(p => p.Name))
        {
            Console.WriteLine($"Name: {plugin.Name}");
            Console.WriteLine($"Version: {plugin.Version}");
            Console.WriteLine($"Installed: {plugin.InstalledDate:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Path: {plugin.InstallPath}");
            if (!string.IsNullOrEmpty(plugin.Description))
            {
                Console.WriteLine($"Description: {plugin.Description}");
            }
            Console.WriteLine();
        }

        return 0;
    }

    static async Task<int> HandleConfigCommand(Dictionary<string, string> options)
    {
        var subCommand = options.GetValueOrDefault("subcommand", "show");

        if (subCommand == "show")
        {
            var configService = new ConfigurationService();
            var result = await configService.LoadConfigAsync();

            if (!result.Success)
            {
                Console.WriteLine($"Error loading configuration: {result.Message}");
                return 1;
            }

            var config = result.Data!;
            Console.WriteLine("Current configuration:");
            Console.WriteLine($"GitHub Owner: {config.GitHubOwner}");
            Console.WriteLine($"GitHub Repo: {config.GitHubRepo}");
            Console.WriteLine($"GitHub Token: {(string.IsNullOrEmpty(config.GitHubToken) ? "Not set" : "***")}");
            Console.WriteLine($"Plugins Directory: {config.PluginsDirectory}");
            Console.WriteLine($"Temp Directory: {config.TempDirectory}");
            Console.WriteLine($"Auto Create Directories: {config.AutoCreateDirectories}");
            Console.WriteLine($"Verify Checksums: {config.VerifyChecksums}");
            Console.WriteLine($"Backup Before Update: {config.BackupBeforeUpdate}");
            Console.WriteLine($"Installed Plugins: {config.InstalledPlugins.Count}");
            return 0;
        }
        else if (subCommand == "set")
        {
            var configService = new ConfigurationService();
            var loadResult = await configService.LoadConfigAsync();

            if (!loadResult.Success)
            {
                Console.WriteLine($"Error loading configuration: {loadResult.Message}");
                return 1;
            }

            var updateResult = await configService.UpdateConfigAsync(config =>
            {
                if (options.ContainsKey("owner")) config.GitHubOwner = options["owner"];
                if (options.ContainsKey("repo")) config.GitHubRepo = options["repo"];
                if (options.ContainsKey("token")) config.GitHubToken = options["token"];
                if (options.ContainsKey("plugins-dir")) config.PluginsDirectory = options["plugins-dir"];
                if (options.ContainsKey("temp-dir")) config.TempDirectory = options["temp-dir"];
            });

            if (updateResult.Success)
            {
                Console.WriteLine("✓ Configuration updated successfully");
                return 0;
            }
            else
            {
                Console.WriteLine($"✗ {updateResult.Message}");
                return 1;
            }
        }
        else
        {
            Console.WriteLine($"Unknown config subcommand: {subCommand}");
            Console.WriteLine("Available subcommands: show, set");
            return 1;
        }
    }

    static async Task<LauncherConfig?> LoadConfigurationAsync(Dictionary<string, string> options)
    {
        var configPath = options.GetValueOrDefault("config");
        var configService = new ConfigurationService(configPath);
        var result = await configService.LoadConfigAsync();

        if (!result.Success)
        {
            Console.WriteLine($"Error loading configuration: {result.Message}");
            return null;
        }

        var config = result.Data!;

        // Override with command line parameters
        if (options.ContainsKey("owner")) config.GitHubOwner = options["owner"];
        if (options.ContainsKey("repo")) config.GitHubRepo = options["repo"];
        if (options.ContainsKey("token")) config.GitHubToken = options["token"];

        // Validate required settings
        if (string.IsNullOrEmpty(config.GitHubOwner) || string.IsNullOrEmpty(config.GitHubRepo))
        {
            Console.WriteLine("Error: GitHub owner and repository must be configured.");
            Console.WriteLine("Use 'config set --owner <owner> --repo <repo>' or provide via command line options.");
            return null;
        }

        return config;
    }

    static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>();
        
        // Handle config subcommand special case
        if (args.Length > 1 && args[0] == "config")
        {
            options["subcommand"] = args[1];
            // Start parsing from index 2
            args = args.Skip(2).ToArray();
        }
        else
        {
            // Skip first argument (command) and look for name parameter
            args = args.Skip(1).ToArray();
            
            // Look for non-option argument as name
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-") && !options.ContainsKey("name"))
                {
                    options["name"] = args[i];
                    // Remove this argument from further processing
                    args = args.Where((item, index) => index != i).ToArray();
                    break;
                }
            }
        }
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    options[key] = args[i + 1];
                    i++; // Skip the value
                }
                else
                {
                    options[key] = "true";
                }
            }
            else if (args[i].StartsWith("-"))
            {
                var key = args[i][1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    options[key] = args[i + 1];
                    i++; // Skip the value
                }
                else
                {
                    options[key] = "true";
                }
            }
        }

        return options;
    }

    static int ShowHelp()
    {
        Console.WriteLine("Usage: PluginLauncher <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                    List available plugins from GitHub");
        Console.WriteLine("  install                 Install a plugin");
        Console.WriteLine("  update                  Update an installed plugin");
        Console.WriteLine("  remove                  Remove an installed plugin");
        Console.WriteLine("  status                  Show status of installed plugins");
        Console.WriteLine("  config                  Manage launcher configuration");
        Console.WriteLine("  help                    Show this help message");
        Console.WriteLine();
        Console.WriteLine("Global Options:");
        Console.WriteLine("  --owner <owner>         GitHub repository owner");
        Console.WriteLine("  --repo <repo>           GitHub repository name");
        Console.WriteLine("  --token <token>         GitHub access token (optional)");
        Console.WriteLine("  --config <path>         Path to configuration file directory");
        Console.WriteLine();
        Console.WriteLine("Install Options:");
        Console.WriteLine("  --name <name>           Name of the plugin to install");
        Console.WriteLine("  --version <version>     Specific version to install (latest if not specified)");
        Console.WriteLine();
        Console.WriteLine("Update/Remove Options:");
        Console.WriteLine("  --name <name>           Name of the plugin");
        Console.WriteLine();
        Console.WriteLine("Config Commands:");
        Console.WriteLine("  config show             Show current configuration");
        Console.WriteLine("  config set              Set configuration values");
        Console.WriteLine();
        Console.WriteLine("Config Set Options:");
        Console.WriteLine("  --owner <owner>         Set GitHub repository owner");
        Console.WriteLine("  --repo <repo>           Set GitHub repository name");
        Console.WriteLine("  --token <token>         Set GitHub access token");
        Console.WriteLine("  --plugins-dir <path>    Set plugins directory path");
        Console.WriteLine("  --temp-dir <path>       Set temporary directory path");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  PluginLauncher config set --owner myuser --repo myplugins");
        Console.WriteLine("  PluginLauncher list");
        Console.WriteLine("  PluginLauncher install --name MyPlugin");
        Console.WriteLine("  PluginLauncher install --name MyPlugin --version v1.0.0");
        Console.WriteLine("  PluginLauncher update --name MyPlugin");
        Console.WriteLine("  PluginLauncher remove --name MyPlugin");
        Console.WriteLine("  PluginLauncher status");
        return 0;
    }

    static int ShowError(string message)
    {
        Console.WriteLine($"Error: {message}");
        Console.WriteLine("Use 'help' command for usage information.");
        return 1;
    }

    static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}
