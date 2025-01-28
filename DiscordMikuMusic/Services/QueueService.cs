using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;

namespace DiscordMikuMusic.Services
{
    internal class QueueService : IQueueService
    {
        private List<Song> _queue = new List<Song>();
        private int _currentIndex = 0;
        private bool _isLoop = false;
        private MikuAudioService? _mikuAudioService;

        public void SetMikuAudioService(MikuAudioService mikuAudioService)
        {
            _mikuAudioService = mikuAudioService;
            _mikuAudioService.OnFinishedPlayingSong += OnFinishedPlayingSong;
        }

        public void RemoveMikuAudioService()
        {
            if (_mikuAudioService is null)
                throw new InvalidOperationException("Audio Service does not exist");

            _mikuAudioService.OnFinishedPlayingSong -= OnFinishedPlayingSong;
            _mikuAudioService = null;
        }

        public bool GetLoopState() => _isLoop;
        public void SetLoop(bool loop) => _isLoop = loop;
        public void AddToQueue(Song song)
        {
            _queue.Add(song);
        }
        public void AddToQueue(List<Song> songs)
        {
            _queue.AddRange(songs);
        }

        public void ResetIndex() => _currentIndex = 0;
        public bool Play()
        {
            if (_mikuAudioService is null)
                throw new InvalidOperationException("Audio Service does not exist");

            if (IsEmpty())
                return false;

            if (_isLoop && _currentIndex >= _queue.Count())
                _currentIndex = 0;

            var song = _queue[_currentIndex];
            _ = _mikuAudioService.Play(song);
            return true;
        }

        public void Remove(int index)
        {
            if (_currentIndex >= _queue.Count)
                throw new InvalidOperationException("Queue is empty");

            _queue.RemoveAt(index);
        }

        public void ClearQueue()
        {
            _queue.Clear();
            _currentIndex = 0;
        }

        public bool IsEmpty() => _queue.Count == 0;

        public List<Song> GetQueue() => _queue;

        public int SongPosition(Song song) => _queue.IndexOf(song);

        public void OnFinishedPlayingSong()
        {
            if (!_isLoop)
                Remove(_currentIndex);
            else
                _currentIndex++;

            Play();
        }

        public int GetCurrentIndex() => _currentIndex;
    }
}
