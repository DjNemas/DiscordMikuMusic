using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    public interface ILibOpusUpdaterService
    {
        Task<NuGetPackageInfoDTO?> TryUpdate();
    }
}
