using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    public interface ILibDaveUpdaterService
    {
        Task<GitHubReleaseResponseDTO?> TryUpdate();
    }
}
