using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Interfaces
{
    internal interface IMikuAudioService
    {
        void Dispose();
        bool IsPlaying();
        Task Play(Song song);
        void Stop();
    }
}