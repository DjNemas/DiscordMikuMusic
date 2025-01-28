using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace DiscordMikuMusic.Services
{
    internal class YoutubeService
    {
        private static string _ytdlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        private static string _ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        private static string _outputPath = Path.Combine(AppContext.BaseDirectory, "output");

        private YoutubeDL _youtubeDL;
        public YoutubeService()
        {
            _youtubeDL = new YoutubeDL();
            _youtubeDL.YoutubeDLPath = _ytdlpPath;
            _youtubeDL.FFmpegPath = _ffmpegPath;
            _youtubeDL.OutputFolder = _outputPath;

            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
        }

        public async Task<RunResult<VideoData>> GetMetadata(string url)
        { 
            return await _youtubeDL.RunVideoDataFetch(url);
        }

        public async Task<RunResult<string>> DownloadAudio(string url)
        {
            // CARE ABOUT THIS LATER
            //var option = OptionSet.Default;
            //if(option.PostprocessorArgs is null)
            //    option.PostprocessorArgs = new MultiValue<string>();
            //option.PostprocessorArgs.Values.Add("-af loudnorm=I=-10:TP=-1.5:LRA=11");

            string output = Path.Combine(_outputPath, "%(title)s.%(ext)s");
            return await _youtubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);
        }

        public async Task<RunResult<string[]>> DownloadPlaylistAudio(string url)
        {
            return await _youtubeDL.RunAudioPlaylistDownload(url, format: AudioConversionFormat.Mp3);
        }
    }
}
