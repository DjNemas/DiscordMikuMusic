using Discord;
using Discord.Interactions;
using DiscordMikuMusic.Services;
using DiscordMikuMusic.Validations;
using YoutubeDLSharp;

namespace DiscordMikuMusic.Interactions.SlashCommands
{
    public class MusicSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("test", "Does it work?!")]
        public async Task PingAsync(string url)
        {
            if (!YoutubeURLValidator.Validate(url))
            {
                await RespondAsync("Invalid URL");
                return;
            }

        }

        [SlashCommand("join", "Let Miku join your Voice Channel!")]
        public async Task JoinAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if(mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }
                
            if (mikuState.GetJoinedVoice())
            {
                await RespondAsync("Already Joined a Voice Channel");
                return;
            }

            // Get the audio channel
            var channel = (Context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) 
            { 
                await RespondAsync("You have to be in a Voice Channel, otherwise Miku can't join you :c");
                return; 
            }

            var audioClient = await channel.ConnectAsync();

            mikuState.CreateAudioService(audioClient);
            mikuState.SetJoinedVoice(true);

            await RespondAsync($"Joined Voice Channel {channel.Name}! 🎶");
        }

        [SlashCommand("leave", "Miku says bye from Voice Channel :c")]
        public async Task LeaveAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if(mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            if (!mikuState.GetJoinedVoice())
            {
                await RespondAsync("Miku is not in a Voice Channel");
                return;
            }

            mikuState.SetJoinedVoice(false);
            mikuState.GetAudioService().Dispose();
            mikuState.RemoveAudioService();

            await RespondAsync("Bye Bye <a:MikuWaving:723334361113427989>");
        }

        [SlashCommand("playyoutube", "Play a Youtube Link")]
        public async Task PlayAsync(string youtubeUrl)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            if (!mikuState.GetJoinedVoice())
            {
                await RespondAsync("Miku is not in a Voice Channel");
                return;
            }

            if(!YoutubeURLValidator.Validate(youtubeUrl))
            {
                await RespondAsync("Invalid URL");
                return;
            }

            var audioService = mikuState.GetAudioService();
            if (audioService.IsPlaying())
                audioService.Stop();

            _ = Task.Run(() => DownloadYoutubeAsync(Context, youtubeUrl, audioService));

            await DeferAsync();
        }

        [SlashCommand("stop", "Miku Miku Miiiii2")]
        public async Task StopAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            if (!mikuState.GetJoinedVoice())
            {
                await RespondAsync("Miku is not in a Voice Channel");
                return;
            }

            var audioService = mikuState.GetAudioService();
            audioService.Stop();

            await RespondAsync("♪♫");
            await DeleteOriginalResponseAsync();
        }

        private async Task DownloadYoutubeAsync(SocketInteractionContext context, string youtubeUrl, MikuAudioService mikuAudio)
        {
            var ytSerivce = new YoutubeService();
            var result = await ytSerivce.DownloadAudio(youtubeUrl);
            if (!result.Success)
            {
                await context.Interaction.RespondAsync("Something went wrong");
                return;
            }
            var testfile = Path.Combine(AppContext.BaseDirectory, result.Data);
            _ = mikuAudio.PlayMp3(testfile);

            await context.Interaction.RespondAsync("♪♫");
            await context.Interaction.DeleteOriginalResponseAsync();
            return;
        }
    }
}
