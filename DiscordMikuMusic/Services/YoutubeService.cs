using System.Diagnostics;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;

namespace DiscordMikuMusic.Services
{
    internal class YoutubeService
    {
        private static readonly string _ytdlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        private static readonly string _ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

        private readonly YoutubeDL _youtubeDL;

        public YoutubeService()
        {
            _youtubeDL = new YoutubeDL
            {
                YoutubeDLPath = _ytdlpPath,
                FFmpegPath = _ffmpegPath
            };
        }

        public async Task<RunResult<VideoData>> GetMetadata(string url)
        {
            return await _youtubeDL.RunVideoDataFetch(url);
        }

        public async Task<Process> CreateAudioStreamProcess(string url)
        {
            var streamUrl = await GetDirectStreamUrl(url);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-hide_banner -loglevel warning -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{streamUrl}\" -f s16le -ar 48000 -ac 2 pipe:1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            return process;
        }

        private async Task<string> GetDirectStreamUrl(string url)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ytdlpPath,
                    Arguments = $"-g -f bestaudio \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var streamUrl = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(streamUrl))
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"yt-dlp failed to get stream URL: {error}");
            }

            return streamUrl;
        }
    }
}
