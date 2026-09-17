using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    public interface ILibSodiumUpdaterService
    {
        Task<NuGetPackageInfoDTO?> TryUpdate();
    }
}
