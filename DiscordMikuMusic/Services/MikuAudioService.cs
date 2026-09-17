using Discord.Audio;
using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DiscordMikuMusic.Services
{
    internal class MikuAudioService : IDisposable, IMikuAudioService
    {
        public event Action? OnFinishedPlayingSong;

        private readonly ILogger _logger;
        private readonly YoutubeService _youtubeService;
        private IAudioClient _audioClient;
        private CancellationTokenSource? _cancellationTokenSource;
        private Process? _ffmpegProcess;

        private bool _isPlaying = false;
        private readonly bool _debugEnabled;

        public MikuAudioService(ILogger logger, IAudioClient audioClient, IConfiguration config)
        {
            _logger = logger;
            _audioClient = audioClient;
            _youtubeService = new YoutubeService();
            _debugEnabled = config.GetValue<bool>("Debug:MikuAudioService");

            if (_debugEnabled) DebugCheckLibDave();
            if (_debugEnabled) DebugRegisterClientEvents();
        }

        public async Task Play(Song song)
        {
            _isPlaying = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (_debugEnabled) DebugLogClientState("Before CreatePCMStream");

                using var audioOutStream = _audioClient.CreatePCMStream(AudioApplication.Music);

                if (_debugEnabled) DebugLogClientState("After CreatePCMStream");

                _ffmpegProcess = await _youtubeService.CreateAudioStreamProcess(song.Url);
                var ffmpegStream = _ffmpegProcess.StandardOutput.BaseStream;

                var stderrTask = DrainFfmpegStderr();

                var buffer = new byte[3840]; // 20ms of 48kHz 16-bit stereo
                int bytesRead;

                if (_debugEnabled) DebugResetStats();

                while ((bytesRead = await ffmpegStream.ReadAsync(buffer, _cancellationTokenSource.Token)) > 0)
                {
                    if (_debugEnabled) DebugAnalyzeChunk(buffer, bytesRead);

                    await audioOutStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancellationTokenSource.Token);
                }

                if (_debugEnabled) await DebugLogFinalSummary(stderrTask);

                await audioOutStream.FlushAsync();
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while playing song: {SongTitle}", song.Title);
            }
            finally
            {
                KillFfmpeg();
                _isPlaying = false;
            }

            OnFinishedPlayingSong?.Invoke();
        }

        public bool IsPlaying() => _isPlaying;

        public void Stop()
        {
            if (_isPlaying)
            {
                _cancellationTokenSource?.Cancel();
            }
        }

        public void Dispose()
        {
            if (_isPlaying)
                Stop();
            KillFfmpeg();
            _audioClient.Dispose();
        }

        private void KillFfmpeg()
        {
            if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
            {
                try { _ffmpegProcess.Kill(); } catch { }
            }
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
        }

        private Task DrainFfmpegStderr()
        {
            return Task.Run(async () =>
            {
                using var reader = _ffmpegProcess!.StandardError;
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    if (_debugEnabled)
                        _logger.LogDebug("[ffmpeg stderr] {Line}", line);
                }
            });
        }

        #region DEBUG

        private long _debugTotalBytes;
        private int _debugChunkCount;
        private int _debugSilentChunks;
        private short _debugGlobalMin;
        private short _debugGlobalMax;

        private void DebugCheckLibDave()
        {
            try
            {
                Discord.LibDave.Dave.SetLogSink((severity, filePath, lineNumber, message) =>
                {
                    _logger.LogDebug("[libdave {Severity} @ {File}#{Line}] {Message}", severity, filePath, lineNumber, message);
                });
                _logger.LogDebug("[libdave] Log sink installed via Discord.LibDave.Dave.SetLogSink");
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[libdave] Failed to install log sink: {Error}", ex.Message);
            }
        }

        private void DebugRegisterClientEvents()
        {
            _audioClient.Connected += () =>
            {
                _logger.LogDebug("[AudioClient] Connected event fired");
                return Task.CompletedTask;
            };
            _audioClient.Disconnected += (ex) =>
            {
                _logger.LogDebug("[AudioClient] Disconnected event fired: {Message}", ex?.Message);
                return Task.CompletedTask;
            };
            _audioClient.StreamCreated += (userId, stream) =>
            {
                _logger.LogDebug("[AudioClient] StreamCreated for user {UserId}", userId);
                return Task.CompletedTask;
            };
            _audioClient.StreamDestroyed += (userId) =>
            {
                _logger.LogDebug("[AudioClient] StreamDestroyed for user {UserId}", userId);
                return Task.CompletedTask;
            };
        }

        private void DebugLogClientState(string context)
        {
            _logger.LogDebug(
                "[AudioClient] {Context} — ConnectionState={State}",
                context, _audioClient.ConnectionState);
        }

        private void DebugResetStats()
        {
            _debugTotalBytes = 0;
            _debugChunkCount = 0;
            _debugSilentChunks = 0;
            _debugGlobalMin = short.MaxValue;
            _debugGlobalMax = short.MinValue;
        }

        private void DebugAnalyzeChunk(byte[] buffer, int bytesRead)
        {
            _debugTotalBytes += bytesRead;
            _debugChunkCount++;

            bool isAllZero = true;
            short chunkMin = short.MaxValue;
            short chunkMax = short.MinValue;

            for (int i = 0; i + 1 < bytesRead; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                if (sample != 0) isAllZero = false;
                if (sample < chunkMin) chunkMin = sample;
                if (sample > chunkMax) chunkMax = sample;
            }

            if (isAllZero) _debugSilentChunks++;
            if (chunkMin < _debugGlobalMin) _debugGlobalMin = chunkMin;
            if (chunkMax > _debugGlobalMax) _debugGlobalMax = chunkMax;

            if (_debugChunkCount <= 5 || _debugChunkCount % 500 == 0)
            {
                _logger.LogDebug(
                    "[PCM] Chunk #{Chunk}: {Bytes} bytes | samples min={Min} max={Max} | allZero={AllZero}",
                    _debugChunkCount, bytesRead, chunkMin, chunkMax, isAllZero);
            }
        }

        private async Task DebugLogFinalSummary(Task stderrTask)
        {
            _logger.LogDebug(
                "[PCM] Stream ended. Total: {TotalBytes} bytes, {Chunks} chunks, {SilentChunks} silent chunks, sample range [{Min}, {Max}]",
                _debugTotalBytes, _debugChunkCount, _debugSilentChunks, _debugGlobalMin, _debugGlobalMax);

            if (_ffmpegProcess is not null)
            {
                await _ffmpegProcess.WaitForExitAsync();
                _logger.LogDebug("[ffmpeg] Exit code: {ExitCode}", _ffmpegProcess.ExitCode);
            }

            await stderrTask;
        }

        #endregion
    }
}
