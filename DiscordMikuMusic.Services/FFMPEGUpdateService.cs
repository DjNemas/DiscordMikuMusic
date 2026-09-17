using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using DiscordMikuMusic.Services.Helper;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DiscordMikuMusic.Services
{
    public partial class FFMPEGUpdateService : IFFmpegUpdaterService
    {
        private const string _releaseInfoFile = "ffmpeg_release.json";
        private readonly Uri _releasePageUrl = new("https://www.gyan.dev/ffmpeg/builds/#release-builds");
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        private readonly string _runningAssemblyFolder;
        private readonly ILogger<FFMPEGUpdateService> _logger;
        private readonly AppInfo _appInfo;
        private FFmpegReleaseResponseDTO? _localBinaryInfo;

        [GeneratedRegex(@"<span id=""release-version"">([^<]+)</span>")]
        private static partial Regex VersionRegex();

        [GeneratedRegex(@"<a\s+href=""(https://www\.gyan\.dev/ffmpeg/builds/ffmpeg-release-essentials\.zip)"">")]
        private static partial Regex DownloadUrlRegex();

        public FFMPEGUpdateService(ILogger<FFMPEGUpdateService> logger, AppInfo appInfo)
            : this(logger, appInfo, Path.GetDirectoryName(AppContext.BaseDirectory)!) { }

        public FFMPEGUpdateService(ILogger<FFMPEGUpdateService> logger, AppInfo appInfo, string downloadPath)
        {
            _logger = logger;
            _appInfo = appInfo;
            _runningAssemblyFolder = downloadPath;
            _localBinaryInfo = ReadInfosFromFile();
        }

        public async Task<FFmpegReleaseResponseDTO?> TryUpdate()
        {
            _logger.LogInformation("Starting ffmpeg update check...");

            var latestRelease = await FetchLatestReleaseInfo();
            if (latestRelease is null) return null;

            if (!IsUpdateNeeded(latestRelease)) return null;

            var success = await DownloadBinary(latestRelease);
            if (!success)
            {
                _logger.LogError("Update to ffmpeg {Version} failed.", latestRelease.Version);
                return null;
            }

            await SaveReleaseInfoToFile(latestRelease);
            _logger.LogInformation("ffmpeg successfully updated to version {Version}.", latestRelease.Version);
            return latestRelease;
        }

        private async Task<FFmpegReleaseResponseDTO?> FetchLatestReleaseInfo()
        {
            _logger.LogDebug("Fetching latest ffmpeg release info from {Url}", _releasePageUrl.ToString());

            var request = new HttpRequestMessage(HttpMethod.Get, _releasePageUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch ffmpeg release page. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();

            var versionMatch = VersionRegex().Match(html);
            if (!versionMatch.Success)
            {
                _logger.LogError("Failed to parse ffmpeg version from release page.");
                return null;
            }

            var downloadMatch = DownloadUrlRegex().Match(html);
            if (!downloadMatch.Success)
            {
                _logger.LogError("Failed to parse ffmpeg download URL from release page.");
                return null;
            }

            var version = versionMatch.Groups[1].Value.Trim();
            var downloadUrl = downloadMatch.Groups[1].Value.Trim();

            _logger.LogDebug("Latest ffmpeg release found: Version={Version}, DownloadUrl={Url}", version, downloadUrl);

            return new FFmpegReleaseResponseDTO(version, downloadUrl);
        }

        private bool IsUpdateNeeded(FFmpegReleaseResponseDTO latestRelease)
        {
            var localInfo = _localBinaryInfo;

            if (localInfo is null)
            {
                _logger.LogInformation("No local release info found. Update is needed.");
                return true;
            }

            if (!Version.TryParse(localInfo.Version, out var localVersion) ||
                !Version.TryParse(latestRelease.Version, out var remoteVersion))
            {
                _logger.LogWarning("Failed to parse version strings (local={Local}, remote={Remote}). Forcing update.",
                    localInfo.Version, latestRelease.Version);
                return true;
            }

            if (localVersion < remoteVersion)
            {
                _logger.LogInformation("New release detected: local={Local}, remote={Remote}",
                    localInfo.Version, latestRelease.Version);
                return true;
            }

            _logger.LogInformation("ffmpeg is already up to date (version: {Version}).", localInfo.Version);
            return false;
        }

        private async Task<bool> DownloadBinary(FFmpegReleaseResponseDTO info)
        {
            _logger.LogInformation("Downloading ffmpeg {Version} from {Url}", info.Version, info.DownloadUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, info.DownloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appInfo.Name, _appInfo.Version));

            using var client = new HttpClient();
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download ffmpeg archive. Status code: {StatusCode}", response.StatusCode);
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength;

            try
            {
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var archiveStream = new MemoryStream();

                await DownloadHelper.DownloadWithProgressAsync(contentStream, archiveStream, totalBytes);
                archiveStream.Position = 0;

                _logger.LogDebug("Downloaded {Bytes} bytes.", archiveStream.Length);

                return ExtractBinariesFromArchive(archiveStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download or extract ffmpeg binaries");
                return false;
            }
        }

        private bool ExtractBinariesFromArchive(Stream archiveStream)
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

            string[] targetFiles = ["ffmpeg.exe", "ffprobe.exe"];
            var extractedCount = 0;

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                if (!targetFiles.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (!entry.FullName.Contains("bin/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var filePath = Path.Combine(_runningAssemblyFolder, entry.Name);
                _logger.LogDebug("Extracting {Entry} to {FilePath}", entry.FullName, filePath);

                using var entryStream = entry.Open();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                entryStream.CopyTo(fileStream);

                _logger.LogInformation("{FileName} written to {FilePath}", entry.Name, filePath);
                extractedCount++;
            }

            if (extractedCount < targetFiles.Length)
            {
                _logger.LogError("Not all target files found in archive. Expected {Expected}, extracted {Extracted}.",
                    targetFiles.Length, extractedCount);
                return false;
            }

            return true;
        }

        private async Task SaveReleaseInfoToFile(FFmpegReleaseResponseDTO release)
        {
            _logger.LogInformation("Update to ffmpeg {Version} succeeded. Saving release info locally.", release.Version);
            var filePath = Path.Combine(_runningAssemblyFolder, _releaseInfoFile);
            var json = JsonSerializer.Serialize(release, _jsonSerializerOptions);
            await File.WriteAllTextAsync(filePath, json);
            _localBinaryInfo = release;
        }

        private FFmpegReleaseResponseDTO? ReadInfosFromFile()
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
            var info = JsonSerializer.Deserialize<FFmpegReleaseResponseDTO>(text);

            if (info is null)
                _logger.LogWarning("Failed to deserialize local release info file at {FilePath}", filePath);
            else
                _logger.LogInformation("Loaded local ffmpeg release info: Version={Version}", info.Version);

            return info;
        }
    }
}
