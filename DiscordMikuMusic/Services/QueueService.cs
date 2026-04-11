using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using NAudio.Wave;

namespace DiscordMikuMusic.Services
{
    internal class QueueService : IQueueService
    {
        private List<Song> _queue = new List<Song>();
        private MikuAudioService? _mikuAudioService;

        private int _currentIndex = 0;
        private bool _isLoop = false;
        private bool _isShuffle = false;

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

        public bool IsPlaying()
        {
            if (_mikuAudioService is null || !_mikuAudioService.IsPlaying())
                return false;

            return true;
        }
        public void SetCurrentIndex(int index) => _currentIndex = index;
        public int GetCurrentIndex() => _currentIndex;
        public bool GetLoopState() => _isLoop;
        public bool GetShuffleState() => _isShuffle;
        public List<Song> GetQueue() => _queue;
        public bool IsEmpty() => _queue.Count == 0;
        public void ResetIndex() => _currentIndex = 0;
        public string? GetSongTitle(int index)
        {
            return _queue.ElementAtOrDefault(index)?.Title;
        }
        public void SetLoop(bool loop)
        {
            _isLoop = loop;
            if (loop)
                _isShuffle = false;
        }
        public void SetShuffle(bool shuffle)
        {
            _isShuffle = shuffle;
            if (_isShuffle)
                _isLoop = false;

        }
        public void AddToQueue(Song song)
        {
            _queue.Add(song);
        }
        public void AddToQueue(List<Song> songs)
        {
            _queue.AddRange(songs);
        }
        
        public bool Play()
        {
            if (IsPlaying())
                return false;

            if (IsEmpty())
                return false;

            if (IsIndexOutOfRange()) // To Prevent Out of Range if Removed is called
                _currentIndex = _queue.Count() - 1;

            var song = _queue[_currentIndex];
            _ = _mikuAudioService!.Play(song);
            return true;
        }

        public void Skip()
        {
            if (_mikuAudioService is null)
                throw new InvalidOperationException("Audio Service does not exist");

            _mikuAudioService.Stop();
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

        public int GetSongPosition(Song song) => _queue.IndexOf(song);

        public void OnFinishedPlayingSong()
        {
            if (_isShuffle)
            {
                PlayShuffle();
                return;
            }

            if(_isLoop && IsLastSongInQueue())
            {
                PlayNext();
                return;
            }
            else if(IsLastSongInQueue())
                return;

            PlayNext();
        }

        private bool IsIndexOutOfRange() => _currentIndex >= _queue.Count();

        private bool IsLastSongInQueue() => _currentIndex == _queue.Count() - 1;

        private void PlayNext()
        {
            if (IsLastSongInQueue())
                ResetIndex();
            else
                _currentIndex++;
            Play();
        }

        private void PlayShuffle()
        {
            int randomIndex = 0;
            do
                randomIndex = new Random().Next(0, _queue.Count());
            while (randomIndex == _currentIndex);

            _currentIndex = randomIndex;
            Play();
        }
    }
}
