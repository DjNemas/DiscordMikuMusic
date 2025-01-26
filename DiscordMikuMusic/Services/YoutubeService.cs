using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace DiscordMikuMusic.Services
{
    internal class YoutubeService
    {
        private static string ytdlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        private static string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        private static string outputPath = Path.Combine(AppContext.BaseDirectory, "output");

        private YoutubeDL _youtubeDL;
        public YoutubeService()
        {
            _youtubeDL = new YoutubeDL();
            _youtubeDL.YoutubeDLPath = ytdlpPath;
            _youtubeDL.FFmpegPath = ffmpegPath;
            _youtubeDL.OutputFolder = outputPath;

            if(!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
        }

        public async Task<RunResult<string>> DownloadAudio(string url)
        {
            //var options = new OptionSet()
            //{
            //    PostprocessorArgs = new[]
            //    {
            //        "-ar 44100 %dl%"
            //    }
            //};

            string output = Path.Combine(outputPath, "%(title)s.%(ext)s");
            return await _youtubeDL.RunAudioDownload(url, AudioConversionFormat.Mp3);
        }
    }
}
