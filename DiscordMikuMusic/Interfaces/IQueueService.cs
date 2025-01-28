using DiscordMikuMusic.Models;
using DiscordMikuMusic.Services;

namespace DiscordMikuMusic.Interfaces
{
    internal interface IQueueService
    {
        void AddToQueue(Song song);
        void ClearQueue();
        int GetCurrentIndex();
        bool GetLoopState();
        List<Song> GetQueue();
        bool IsEmpty();
        void OnFinishedPlayingSong();
        bool Play();
        void Remove(int index);
        void RemoveMikuAudioService();
        void ResetIndex();
        void SetLoop(bool loop);
        void SetMikuAudioService(MikuAudioService mikuAudioService);
        int SongPosition(Song song);
    }
}