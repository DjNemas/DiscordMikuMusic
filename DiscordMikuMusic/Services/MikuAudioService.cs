using Discord.Audio;
using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DiscordMikuMusic.Services
{
    internal class MikuAudioService : IDisposable, IMikuAudioService
    {
        public event Action? OnFinishedPlayingSong;

        private IAudioClient _audioClient;
        private AudioOutStream _audioOutStream;
        private CancellationTokenSource? _cancellationTokenSource;

        private bool _isPlaying = false;

        public MikuAudioService(IAudioClient audioClient)
        {
            _audioClient = audioClient;
            _audioOutStream = _audioClient.CreatePCMStream(AudioApplication.Music);
        }

        public async Task Play(Song song)
        {
            _isPlaying = true;
            _cancellationTokenSource = new CancellationTokenSource();

            using var mp3Reader = new Mp3FileReader(song.FilePath.FullName);
            mp3Reader.Seek(0, SeekOrigin.Begin);
            try
            {
                await mp3Reader.CopyToAsync(_audioOutStream, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            finally
            {
                _audioOutStream.Flush();
            }

            _isPlaying = false;
            OnFinishedPlayingSong?.Invoke();
        }

        public bool IsPlaying() => _isPlaying;

        public void Stop()
        {
            if(_isPlaying)
            {
                _cancellationTokenSource?.Cancel();
            }
                
        }

        public void Dispose()
        {
            if (_isPlaying)
                Stop();
            _audioClient.Dispose();
        }

    }
}
