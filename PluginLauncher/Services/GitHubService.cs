using Newtonsoft.Json;
using PluginLauncher.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace PluginLauncher.Services;

/// <summary>
/// Service for interacting with GitHub API and downloading files
/// </summary>
public class GitHubService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubService(string owner, string repo, string? token = null)
    {
        _owner = owner;
        _repo = repo;
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PluginLauncher", "1.0"));
        
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Get list of releases from GitHub repository
    /// </summary>
    public async Task<OperationResult<List<GitHubRelease>>> GetReleasesAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return OperationResult<List<GitHubRelease>>.CreateFailure(
                    $"Failed to get releases: {response.StatusCode} - {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(content) ?? new List<GitHubRelease>();
            
            return OperationResult<List<GitHubRelease>>.CreateSuccess(releases, 
                $"Retrieved {releases.Count} releases");
        }
        catch (Exception ex)
        {
            return OperationResult<List<GitHubRelease>>.CreateFailure(
                $"Error getting releases: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Download a file from GitHub
    /// </summary>
    public async Task<OperationResult> DownloadFileAsync(string downloadUrl, string destinationPath, 
        IProgress<double>? progress = null)
    {
        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            
            if (!response.IsSuccessStatusCode)
            {
                return OperationResult.CreateFailure(
                    $"Failed to download file: {response.StatusCode} - {response.ReasonPhrase}");
            }

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercentage = (double)downloadedBytes / totalBytes * 100;
                    progress?.Report(progressPercentage);
                }
            }

            return OperationResult.CreateSuccess($"File downloaded successfully to {destinationPath}");
        }
        catch (Exception ex)
        {
            return OperationResult.CreateFailure($"Error downloading file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculate SHA256 checksum of a file
    /// </summary>
    public static async Task<string> CalculateFileChecksumAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// GitHub release information
/// </summary>
public class GitHubRelease
{
    [JsonProperty("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;

    [JsonProperty("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonProperty("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();

    [JsonProperty("prerelease")]
    public bool PreRelease { get; set; }
}

/// <summary>
/// GitHub release asset information
/// </summary>
public class GitHubAsset
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("download_count")]
    public int DownloadCount { get; set; }
}