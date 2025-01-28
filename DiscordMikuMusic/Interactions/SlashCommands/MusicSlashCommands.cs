using Discord;
using Discord.Interactions;
using DiscordMikuMusic.Models;
using DiscordMikuMusic.Services;
using YoutubeDLSharp;

namespace DiscordMikuMusic.Interactions.SlashCommands
{
    public class MusicSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("showcurrentqueue", "Displays the Current Queue")]
        public async Task ShowQueueAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            if (queue.IsEmpty())
            {
                await RespondAsync("Queue is Empty");
                return;
            }

            var builder = new EmbedBuilder();
            builder.WithTitle($"Current Queue - Looping: {mikuState.GetQueueService().GetLoopState()}");
            builder.WithColor(Color.Blue);
            foreach (var item in queue.GetQueue())
            {
                builder.AddField((
                    queue.SongPosition(item) + 1).ToString() +
                    (queue.SongPosition(item) == queue.GetCurrentIndex() ? " - Playing" : string.Empty),
                    item.Title);
            }
            await RespondAsync(embed: builder.Build());
        }

        [SlashCommand("join", "Let Miku join your Voice Channel!")]
        public async Task JoinAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
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

            mikuState.CreateServices(audioClient);
            mikuState.SetJoinedVoice(true);

            await RespondAsync($"Joined Voice Channel {channel.Name}! 🎶");
        }

        [SlashCommand("leave", "Miku says bye from Voice Channel :c")]
        public async Task LeaveAsync()
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

            mikuState.SetJoinedVoice(false);
            mikuState.GetAudioService().Dispose();
            mikuState.RemoveServices();

            await RespondAsync("Bye Bye <a:MikuWaving:723334361113427989>");
        }

        [SlashCommand("addyoutube", "Add a Youtube Link to the Queue")]
        public async Task AddYoutube(string youtubeUrl)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            await DeferAsync();

            var ytSerivce = new YoutubeService();
            var metadata = await ytSerivce.GetMetadata(youtubeUrl);
            if (!metadata.Success)
            {
                foreach (var error in metadata.ErrorOutput)
                    Console.WriteLine($"[YTDownloader] {error}");
                await FollowupAsync("Invalid Youtube Link");
                return;
            }

            var result = await ytSerivce.DownloadAudio(youtubeUrl);
            if (!result.Success)
            {
                foreach (var error in result.ErrorOutput)
                    Console.WriteLine($"[YTDownloader] {error}");

                await FollowupAsync("Something went wrong");
                return;
            }

            var song = new Song()
            {
                Title = metadata.Data.Title,
                FilePath = new FileInfo(result.Data)
            };

            mikuState.GetQueueService().AddToQueue(song);

            await FollowupAsync($"Song {song.Title} Added ♪♫");
        }

        [SlashCommand("addyoutubeplaylist", "Add a Youtube PLaylist to the Queue LONG LOADING TIME")]
        public async Task AddYoutubePLaylist(string youtubeUrl)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            await DeferAsync();

            var ytSerivce = new YoutubeService();
            var metadata = await ytSerivce.GetMetadata(youtubeUrl);
            if (!metadata.Success)
            {
                foreach (var error in metadata.ErrorOutput)
                    Console.WriteLine($"[YTDownloader] {error}");
                await FollowupAsync("Invalid Youtube Link");
                return;
            }

            var tempArray = metadata.Data.Entries.ToArray();

            var songs = new List<Song>();
            var responseErrors = new List<string>();
            var count = 0;
            foreach (var entrie in tempArray)
            {
                var result = await ytSerivce.DownloadAudio(entrie.Url);
                if (!result.Success)
                {
                    foreach (var error in result.ErrorOutput)
                        Console.WriteLine($"[YTDownloader] {error}");

                    responseErrors.Add($"Could not add Video: {entrie.Title}\n");
                    count++;
                    continue;

                }
                var song = new Song()
                {
                    Title = tempArray.ElementAt(count).Title,
                    FilePath = new FileInfo(result.Data)
                };
                songs.Add(song);
                count++;
            }            

            mikuState.GetQueueService().AddToQueue(songs);

            await FollowupAsync($"Playlist {metadata.Data.Title} Added ♪♫{(responseErrors.Count() > 0 ? $"\n{string.Join("", responseErrors)}" : string.Empty)}");
        }

        [SlashCommand("play", "Play a Youtube Link")]
        public async Task PlayAsync(int? SongNumber = null)
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

            var queueService = mikuState.GetQueueService();
            queueService.SetMikuAudioService(mikuState.GetAudioService());

            queueService.ResetIndex();
            var started = queueService.Play();
            if (!started)
            {
                await RespondAsync("Queue is Empty");
                return;
            }

            await RespondAsync($"♪♫");
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

            mikuState.GetAudioService().Stop();
            mikuState.GetQueueService().RemoveMikuAudioService();

            await RespondAsync("♪♫");
            await DeleteOriginalResponseAsync();
        }

        [SlashCommand("loop", "Set if should loop or not")]
        public async Task SetLoop(bool loop)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            queue.SetLoop(loop);
            await RespondAsync($"Loop set to {loop}");
        }
    }
}
