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
    public class LibOpusUpdaterService : ILibOpusUpdaterService
    {
        private const string _packageName = "libopus";
        private const string _releaseInfoFile = "libopus_release.json";
        private const string _nugetFlatContainerBaseUrl = "https://api.nuget.org/v3-flatcontainer";
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        private readonly string _runningAssemblyFolder;
        private readonly ILogger<LibOpusUpdaterService> _logger;
        private readonly AppInfo _appInfo;
        private NuGetPackageInfoDTO? _localPackageInfo;

        public LibOpusUpdaterService(ILogger<LibOpusUpdaterService> logger, AppInfo appInfo)
            : this(logger, appInfo, Path.GetDirectoryName(AppContext.BaseDirectory)!) { }

        public LibOpusUpdaterService(ILogger<LibOpusUpdaterService> logger, AppInfo appInfo, string downloadPath)
        {
            _logger = logger;
            _appInfo = appInfo;
            _runningAssemblyFolder = downloadPath;
            _localPackageInfo = ReadInfosFromFile();
        }

        public async Task<NuGetPackageInfoDTO?> TryUpdate()
        {
            _logger.LogInformation("Starting libopus update check...");

            var latestVersion = await FetchLatestVersion();
            if (latestVersion is null) return null;

            var latestInfo = new NuGetPackageInfoDTO(_packageName, latestVersion);

            if (!IsUpdateNeeded(latestInfo)) return null;

            var success = await DownloadAndExtract(latestInfo);
            if (!success)
            {
                _logger.LogError("Update to libopus {Version} failed.", latestInfo.Version);
                return null;
            }

            await SaveReleaseInfoToFile(latestInfo);
            _logger.LogInformation("libopus successfully updated to version {Version}.", latestInfo.Version);
            return latestInfo;
        }

        private async Task<string?> FetchLatestVersion()
        {
            var indexUrl = $"{_nugetFlatContainerBaseUrl}/{_packageName}/index.json";
            _logger.LogDebug("Fetching latest libopus version from NuGet at {Url}", indexUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, indexUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch NuGet package index. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(json);

            if (node is null)
            {
                _logger.LogError("Failed to parse JSON response from NuGet API.");
                return null;
            }

            var versions = node["versions"]?.AsArray();
            if (versions is null || versions.Count == 0)
            {
                _logger.LogError("No versions found in NuGet package index.");
                return null;
            }

            var latest = versions
                .Select(v => v?.GetValue<string>())
                .Where(v => v is not null && !v.Contains('-'))
                .LastOrDefault();

            if (latest is null)
            {
                _logger.LogError("No stable version found in NuGet package index.");
                return null;
            }

            _logger.LogDebug("Latest libopus NuGet version: {Version}", latest);
            return latest;
        }

        private bool IsUpdateNeeded(NuGetPackageInfoDTO latestInfo)
        {
            var localInfo = _localPackageInfo;

            if (localInfo is null)
            {
                _logger.LogInformation("No local release info found. Update is needed.");
                return true;
            }

            if (!Version.TryParse(localInfo.Version, out var localVersion) ||
                !Version.TryParse(latestInfo.Version, out var remoteVersion))
            {
                _logger.LogWarning("Failed to parse version strings (local={Local}, remote={Remote}). Forcing update.",
                    localInfo.Version, latestInfo.Version);
                return true;
            }

            if (localVersion < remoteVersion)
            {
                _logger.LogInformation("New release detected: local={Local}, remote={Remote}",
                    localInfo.Version, latestInfo.Version);
                return true;
            }

            _logger.LogInformation("libopus is already up to date (version: {Version}).", localInfo.Version);
            return false;
        }

        private async Task<bool> DownloadAndExtract(NuGetPackageInfoDTO info)
        {
            var downloadUrl = $"{_nugetFlatContainerBaseUrl}/{info.PackageName}/{info.Version}/{info.PackageName}.{info.Version}.nupkg";
            _logger.LogInformation("Downloading libopus {Version} from {Url}", info.Version, downloadUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download NuGet package. Status code: {StatusCode}", response.StatusCode);
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength;

            try
            {
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var nupkgStream = new MemoryStream();
                await DownloadHelper.DownloadWithProgressAsync(contentStream, nupkgStream, totalBytes);
                nupkgStream.Position = 0;

                _logger.LogDebug("Downloaded {Bytes} bytes.", nupkgStream.Length);

                return await ExtractDllFromNupkg(nupkgStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download or extract libopus from NuGet package");
                return false;
            }
        }

        private async Task<bool> ExtractDllFromNupkg(Stream nupkgStream)
        {
            using var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read);

            var dllEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Contains("shared/win-x64/native", StringComparison.OrdinalIgnoreCase) &&
                (e.Name.Equals("opus.dll", StringComparison.OrdinalIgnoreCase) ||
                 e.Name.Equals("libopus.dll", StringComparison.OrdinalIgnoreCase)));

            if (dllEntry is null)
            {
                _logger.LogError("opus.dll/libopus.dll not found in shared/win-x64/native/ of the NuGet package.");
                return false;
            }

            _logger.LogDebug("Found {Entry} in NuGet package.", dllEntry.FullName);

            var filePath = Path.Combine(_runningAssemblyFolder, "libopus.dll");
            _logger.LogDebug("Writing binary to {FilePath}", filePath);

            using var entryStream = dllEntry.Open();
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await entryStream.CopyToAsync(fileStream);

            _logger.LogInformation("libopus.dll written to {FilePath}", filePath);
            return true;
        }

        private async Task SaveReleaseInfoToFile(NuGetPackageInfoDTO info)
        {
            _logger.LogInformation("Update to libopus {Version} succeeded. Saving release info locally.", info.Version);
            var filePath = Path.Combine(_runningAssemblyFolder, _releaseInfoFile);
            var json = JsonSerializer.Serialize(info, _jsonSerializerOptions);
            await File.WriteAllTextAsync(filePath, json);
            _localPackageInfo = info;
        }

        private NuGetPackageInfoDTO? ReadInfosFromFile()
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
            var info = JsonSerializer.Deserialize<NuGetPackageInfoDTO>(text);

            if (info is null)
                _logger.LogWarning("Failed to deserialize local release info file at {FilePath}", filePath);
            else
                _logger.LogInformation("Loaded local libopus release info: Version={Version}", info.Version);

            return info;
        }
    }
}
