using System.Diagnostics;

namespace DiscordMikuMusic.Services.Helper
{
    public static class DownloadHelper
    {
        public static async Task DownloadWithProgressAsync(Stream source, Stream destination, long? totalBytes)
        {
            var buffer = new byte[81920];
            long bytesRead = 0;
            var stopwatch = Stopwatch.StartNew();
            int read;

            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                if (totalBytes is > 0)
                {
                    var percentage = (double)bytesRead / totalBytes.Value * 100;
                    var mbDownloaded = bytesRead / (1024.0 * 1024.0);
                    var mbTotal = totalBytes.Value / (1024.0 * 1024.0);

                    const int barWidth = 30;
                    var filled = (int)(percentage / 100 * barWidth);
                    var bar = new string('█', filled) + new string('░', barWidth - filled);

                    var elapsed = stopwatch.Elapsed;
                    string eta;

                    if (bytesRead >= totalBytes.Value)
                    {
                        eta = "Done!  ";
                    }
                    else if (percentage > 1)
                    {
                        var remaining = elapsed / percentage * (100 - percentage);
                        eta = $"ETA: {remaining:mm\\:ss}";
                    }
                    else
                    {
                        eta = "ETA: --:--";
                    }

                    Console.Write($"\r[{bar}] {percentage,5:F1}% | {mbDownloaded,6:F1} / {mbTotal:F1} MB | {eta}");
                }
            }

            Console.WriteLine();
        }
    }
}
