using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using DiscordMikuMusic.Services.Helper;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscordMikuMusic.Services
{
    public class LibDaveUpdaterService : ILibDaveUpdaterService
    {
        private const string _releaseInfoFile = "libdave_release.json";
        private readonly Uri _releaseUrl = new Uri("https://api.github.com/repos/discord/libdave/releases/latest");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        private readonly string _runningAssemblyFolder;
        private readonly ILogger<LibDaveUpdaterService> _logger;
        private readonly AppInfo _appInfo;
        private GitHubReleaseResponseDTO? _localBinaryInfo;

        public LibDaveUpdaterService(ILogger<LibDaveUpdaterService> logger, AppInfo appInfo)
            : this(logger, appInfo, Path.GetDirectoryName(AppContext.BaseDirectory)!) { }

        public LibDaveUpdaterService(ILogger<LibDaveUpdaterService> logger, AppInfo appInfo, string downloadPath)
        {
            _logger = logger;
            _appInfo = appInfo;
            _runningAssemblyFolder = downloadPath;
            _localBinaryInfo = ReadInfosFromFile();
        }

        public async Task<GitHubReleaseResponseDTO?> TryUpdate()
        {
            _logger.LogInformation("Starting libdave update check...");

            var assets = await FetchLatestReleaseAssets();
            if (assets is null) return null;

            var latestRelease = FindTargetAsset(assets);
            if (latestRelease is null) return null;

            if (!IsUpdateNeeded(latestRelease)) return null;

            var success = await DownloadBinary(latestRelease);
            if (!success)
            {
                _logger.LogError("Update to release {CreatedAt} failed.", latestRelease.CreatedAt.ToString(Strings.GermanDateTimeFormat));
                return null;
            }

            await SaveReleaseInfoToFile(latestRelease);
            _logger.LogInformation("libdave successfully updated to release {CreatedAt}.", latestRelease.CreatedAt.ToString(Strings.GermanDateTimeFormat));
            return latestRelease;
        }

        private async Task<GitHubReleaseResponseDTO[]?> FetchLatestReleaseAssets()
        {
            _logger.LogDebug("Fetching latest release info from {Url}", _releaseUrl.ToString());

            var message = new HttpRequestMessage(HttpMethod.Get, _releaseUrl);
            message.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch latest release info. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(json);

            if (node is null)
            {
                _logger.LogError("Failed to parse JSON response from GitHub API.");
                return null;
            }

            var assets = JsonSerializer.Deserialize<GitHubReleaseResponseDTO[]>(node["assets"]!.AsArray());

            if (assets is null)
                _logger.LogError("Failed to deserialize latest release response from GitHub API.");

            return assets;
        }

        private GitHubReleaseResponseDTO? FindTargetAsset(GitHubReleaseResponseDTO[] assets)
        {
            var asset = assets.FirstOrDefault(a => a.Name.EndsWith("libdave-Windows-X64-boringssl.zip", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                _logger.LogError("No suitable libdave asset found in the latest release.");
                return null;
            }

            _logger.LogDebug("Latest release info found: CreatedAt={CreatedAt}, UpdatedAt={UpdatedAt}",
                asset.CreatedAt.ToString(Strings.GermanDateTimeFormat), asset.UpdatedAt.ToString(Strings.GermanDateTimeFormat));

            return asset;
        }

        private bool IsUpdateNeeded(GitHubReleaseResponseDTO latestRelease)
        {
            var localInfo = _localBinaryInfo;

            if (localInfo is null)
            {
                _logger.LogInformation("No local release info found. Update is needed.");
                return true;
            }

            if (localInfo.CreatedAt < latestRelease.CreatedAt)
            {
                _logger.LogInformation("New release detected via CreatedAt: local={Local}, remote={Remote}",
                    localInfo.CreatedAt.ToString(Strings.GermanDateTimeFormat), latestRelease.CreatedAt.ToString(Strings.GermanDateTimeFormat));
                return true;
            }

            if (localInfo.UpdatedAt < latestRelease.UpdatedAt)
            {
                _logger.LogInformation("New release detected via UpdatedAt: local={Local}, remote={Remote}",
                    localInfo.UpdatedAt.ToString(Strings.GermanDateTimeFormat), latestRelease.UpdatedAt.ToString(Strings.GermanDateTimeFormat));
                return true;
            }

            _logger.LogInformation("libdave is already up to date (released: {CreatedAt}).", localInfo.CreatedAt.ToString(Strings.GermanDateTimeFormat));
            return false;
        }

        private async Task<bool> DownloadBinary(GitHubReleaseResponseDTO info)
        {
            _logger.LogInformation("Downloading libdave release {CreatedAt} from {Url}", info.CreatedAt.ToString(Strings.GermanDateTimeFormat), info.DownloadUrl);

            var message = new HttpRequestMessage(HttpMethod.Get, info.DownloadUrl);
            message.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download libdave binary. Status code: {StatusCode}", response.StatusCode);
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength;

            try
            {
                using var networkStream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await DownloadHelper.DownloadWithProgressAsync(networkStream, memoryStream, totalBytes);

                _logger.LogDebug("Downloaded {Bytes} bytes.", memoryStream.Length);

                memoryStream.Position = 0;
                return await ExtractDllFromZip(memoryStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download or extract libdave binary");
                return false;
            }
        }

        private async Task<bool> ExtractDllFromZip(Stream zipStream)
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // GitHub release zip structure: bin/libdave.dll
            var dllEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("bin/libdave.dll", StringComparison.OrdinalIgnoreCase));

            if (dllEntry is null)
            {
                _logger.LogError("libdave.dll not found in bin/ of the release archive.");
                return false;
            }

            _logger.LogDebug("Found {Entry} in release archive.", dllEntry.FullName);

            var filePath = Path.Combine(_runningAssemblyFolder, "libdave.dll");
            _logger.LogDebug("Writing binary to {FilePath}", filePath);

            using var entryStream = dllEntry.Open();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await entryStream.CopyToAsync(fileStream);

            _logger.LogInformation("libdave.dll written to {FilePath}", filePath);
            return true;
        }

        private async Task SaveReleaseInfoToFile(GitHubReleaseResponseDTO release)
        {
            _logger.LogInformation("Update to release {CreatedAt} succeeded. Saving release info locally.", release.CreatedAt.ToString(Strings.GermanDateTimeFormat));
            var filePath = Path.Combine(_runningAssemblyFolder, _releaseInfoFile);
            var json = JsonSerializer.Serialize(release, _jsonSerializerOptions);
            await File.WriteAllTextAsync(filePath, json);
            _localBinaryInfo = release;
        }

        private GitHubReleaseResponseDTO? ReadInfosFromFile()
        {
            var filePath = Path.Combine(_runningAssemblyFolder, _releaseInfoFile);
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
                _logger.LogInformation("Loaded local release info: CreatedAt={CreatedAt}", info.CreatedAt.ToString(Strings.GermanDateTimeFormat));

            return info;
        }
    }
}
