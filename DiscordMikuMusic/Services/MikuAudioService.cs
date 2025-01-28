using Discord.Audio;
using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using NAudio.Wave;

namespace DiscordMikuMusic.Services
{
    internal class MikuAudioService : IDisposable, IMikuAudioService
    {
        public Action? OnFinishedPlayingSong;

        private IAudioClient _audioClient;
        private AudioOutStream _audioOutStream;
        private CancellationTokenSource? _cancellationTokenSource;

        private bool _isPlaying = false;

        public MikuAudioService(IAudioClient audioClient)
        {
            _audioClient = audioClient;
            _audioOutStream = audioClient.CreatePCMStream(AudioApplication.Music);
        }

        public async Task Play(Song song)
        {
            _isPlaying = true;
            _cancellationTokenSource = new CancellationTokenSource();


            using (var mp3Reader = new Mp3FileReader(song.FilePath.FullName))
            using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader))
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                try
                {
                    while ((bytesRead = await pcmStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token)) > 0)
                    {
                        await _audioOutStream.WriteAsync(buffer, 0, bytesRead, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation
                }
            }

            _isPlaying = false;
            if(!_cancellationTokenSource.IsCancellationRequested)
                OnFinishedPlayingSong?.Invoke();
        }

        public bool IsPlaying() => _isPlaying;

        public void Stop()
        {
            _isPlaying = false;
            _cancellationTokenSource?.Cancel();
            _audioOutStream.Flush();
        }

        public void Dispose()
        {
            if (_isPlaying)
                Stop();
            _audioClient.Dispose();
        }

    }
}
