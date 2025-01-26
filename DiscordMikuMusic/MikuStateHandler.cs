﻿using Discord;
using Discord.Audio;
using DiscordMikuMusic.Services;

namespace DiscordMikuMusic
{
    internal class MikuStateHandler
    {
        private static List<MikuStateHandler> _guildsList = new List<MikuStateHandler>();

        private IGuild _guildID;
        private bool _joinedVoice;
        private MikuAudioService? _audioService;

        internal MikuStateHandler(IGuild guildID)
        {
            _guildID = guildID;
            _joinedVoice = false;
        }
        
        public void SetJoinedVoice(bool joined) => _joinedVoice = joined;
        public bool GetJoinedVoice() => _joinedVoice;

        public void CreateAudioService(IAudioClient audioClient)
        {
            if(_audioService is null)
                _audioService = new MikuAudioService(audioClient);
            else
                throw new InvalidOperationException("Audio Service already exists");
        }

        public MikuAudioService GetAudioService()
        {
            if (_audioService is null)
                throw new InvalidOperationException("Audio Service does not exist");
            return _audioService;
        }

        public void RemoveAudioService()
        {
            if (_audioService is null)
                throw new InvalidOperationException("Audio Service does not exist");
            _audioService.Dispose();
            _audioService = null;
        }

        // Statics
        public static void RemoveState(MikuStateHandler mikuStateHandler)
        {
            _guildsList.Remove(mikuStateHandler);
        }

        public static MikuStateHandler CreateState(IGuild guild)
        {
            var newState = new MikuStateHandler(guild);
            _guildsList.Add(newState);
            return newState;
        }

        public static MikuStateHandler? GetState(IGuild guild)
        {
            return _guildsList.Find(x => x._guildID == guild);
        }

    }


}
