using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscordMikuMusic.Services
{
    public class YTDLPUpdaterService : IYTDLPUpdaterService
    {
        private const string ReleaseInfoFile = "ytdlp_release.json";
        private readonly Uri _releaseUrl = new Uri("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");

        private readonly string _runningAssemblyFolder = Path.GetDirectoryName(AppContext.BaseDirectory)!;
        private readonly ILogger<YTDLPUpdaterService> _logger;
        private GitHubReleaseResponseDTO? _localBinaryInfo;

        public YTDLPUpdaterService(ILogger<YTDLPUpdaterService> logger)
        {
            _logger = logger;
            _localBinaryInfo = ReadInfosFromFile();
        }

        public async Task<GitHubReleaseResponseDTO?> TryUpdate()
        {
            _logger.LogInformation("Starting yt-dlp update check...");            
            _logger.LogDebug("Fetching latest release info from {Url}", _releaseUrl.ToString());

            var message = new HttpRequestMessage(HttpMethod.Get, _releaseUrl);
            message.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("DiscordMikuMusic", "1.0"));

            using var client = new HttpClient();
            var response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch latest release info. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var node = JsonObject.Parse(json);
            
            if (node is null)
            {
                _logger.LogError("Failed to parse JSON response from GitHub API.");
                return null;
            }
            
            var latestReleaseAssets = JsonSerializer.Deserialize<GitHubReleaseResponseDTO[]>(node["assets"]!.AsArray());

            if (latestReleaseAssets is null)
            {
                _logger.LogError("Failed to deserialize latest release response from GitHub API.");
                return null;
            }

            var latestRelease = latestReleaseAssets.FirstOrDefault(asset => asset.Name.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase));
            if (latestRelease is null)
            {
                _logger.LogError("No suitable yt-dlp.exe asset found in the latest release.");
                return null;
            }

            _logger.LogDebug("Latest release info found: Name={Name}, CreatedAt={CreatedAt}, UpdatedAt={UpdatedAt}",
                latestRelease.Name, latestRelease.CreatedAt, latestRelease.UpdatedAt);

            var localInfo = _localBinaryInfo;
            var needsUpdate = false;
            if(localInfo is null)
            {
                _logger.LogInformation("No local release info found. Update is needed.");
                needsUpdate = true;
            }
            else if (localInfo.CreatedAt < latestRelease.CreatedAt)
            {
                _logger.LogInformation("New release detected via CreatedAt: local={Local}, remote={Remote}",
                    localInfo.CreatedAt, latestRelease.CreatedAt);
                needsUpdate = true;
            }
            else if (localInfo.UpdatedAt < latestRelease.UpdatedAt)
            {
                _logger.LogInformation("New release detected via UpdatedAt: local={Local}, remote={Remote}",
                    localInfo.UpdatedAt, latestRelease.UpdatedAt);
                needsUpdate = true;
            }

            if (!needsUpdate)
            {
                _logger.LogInformation("yt-dlp is already up to date (version: {Name}).", localInfo!.Name);
                return null;
            }

            var success = await Update(latestRelease);

            if (!success)
            {
                _logger.LogError("Update to version {Name} failed.", latestRelease.Name);
                return null;
            }

            _logger.LogInformation("yt-dlp successfully updated to version {Name}.", latestRelease.Name);
            return latestRelease;
        }

        private async Task<bool> Update(GitHubReleaseResponseDTO info)
        {
            _logger.LogInformation("Downloading yt-dlp version {Name} from {Url}", info.Name, info.DownloadUrl);

            var message = new HttpRequestMessage(HttpMethod.Get, info.DownloadUrl);
            message.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("DiscordMikuMusic", "1.0"));

            using var client = new HttpClient();
            var response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download yt-dlp binary. Status code: {StatusCode}", response.StatusCode);
                return false;
            }

            try
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogDebug("Downloaded {Bytes} bytes.", bytes.Length);

                var filePath = Path.Combine(_runningAssemblyFolder, "yt-dlp.exe");
                _logger.LogDebug("Writing binary to {FilePath}", filePath);

                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogInformation("yt-dlp binary written to {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write yt-dlp binary to disk");
                return false;
            }
        }

        private GitHubReleaseResponseDTO? ReadInfosFromFile()
        {
            var filePath = Path.Combine(_runningAssemblyFolder, ReleaseInfoFile);
            _logger.LogDebug("Looking for local release info file at {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Local release info file not found at {FilePath}", filePath);
                return null;
            }

            _logger.LogDebug("Reading local release info from {FilePath}", filePath);
            var text = File.ReadAllText(filePath);
            var info = JsonSerializer.Deserialize<GitHubReleaseResponseDTO>(text);

            if (info is null)
                _logger.LogWarning("Failed to deserialize local release info file at {FilePath}", filePath);
            else
                _logger.LogInformation("Loaded local release info: Name={Name}, CreatedAt={CreatedAt}", info.Name, info.CreatedAt);

            return info;
        }
    }
}
