using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    public interface IFFmpegUpdaterService
    {
        Task<FFmpegReleaseResponseDTO?> TryUpdate();
    }
}
