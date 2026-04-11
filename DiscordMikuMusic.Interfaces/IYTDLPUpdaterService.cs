using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    public interface IYTDLPUpdaterService
    {
        Task<GitHubReleaseResponseDTO?> TryUpdate();
    }
}
