using Discord;
using Discord.Audio;
using Discord.Interactions;
using DiscordMikuMusic.Models;
using DiscordMikuMusic.Services;
using DiscordMikuMusic.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YoutubeDLSharp.Metadata;

namespace DiscordMikuMusic.Interactions.SlashCommands
{
    public class MusicSlashCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<MusicSlashCommands> _logger;
        private readonly EmojiReactionService _emojiReactionService;
        private readonly IConfiguration _config;

        public MusicSlashCommands(ILogger<MusicSlashCommands> logger, EmojiReactionService emojiReactionService, IConfiguration config)
        {
            _logger = logger;
            _emojiReactionService = emojiReactionService;
            _config = config;
        }

        [SlashCommand("show_queue", "Displays the Current Queue")]
        public async Task ShowQueueAsync(int page = 1)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            var allSongs = queue.GetQueue().ToList();
            if (!allSongs.Any())
            {
                await RespondAsync("Queue is Empty");
                return;
            }

            const int pageSize = 25;
            int totalPages = (int)Math.Ceiling(allSongs.Count / (double)pageSize);
            page = Math.Clamp(page, 1, totalPages);
            int start = (page - 1) * pageSize;
            var pageSongs = allSongs.Skip(start).Take(pageSize).ToList();

            var builder = new EmbedBuilder();
            builder.WithTitle($"Current Queue (Page {page}/{totalPages})\n" +
                $"Loop: {queue.GetLoopState()}\n" +
                $"Shuffle: {queue.GetShuffleState()}");
            builder.WithColor(Color.Blue);
            foreach (var item in pageSongs)
            {
                builder.AddField((queue.GetSongPosition(item) + 1).ToString() +
                    (queue.GetSongPosition(item) == queue.GetCurrentIndex() ? " - Playing" : string.Empty),
                    item.Title);
            }

            await RespondAsync(embed: builder.Build());

            // Add reactions for paging if needed (max 25 fields per page)
            if (totalPages > 1)
            {
                var sentMsg = await Context.Interaction.GetOriginalResponseAsync();
                if (page > 1)
                    await sentMsg.AddReactionAsync(new Emoji("⬅️"));
                if (page < totalPages)
                    await sentMsg.AddReactionAsync(new Emoji("➡️"));

                // Register message for reaction service
                _emojiReactionService.RegisterQueueMessage(Context.Guild.Id, sentMsg.Id, page);
            }
        }

        [SlashCommand("join", "Let Miku join your Voice Channel!")]
        public async Task JoinAsync()
        {
            await DeferAsync();

            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await FollowupAsync("Something went wrong");
                return;
            }

            if (mikuState.GetJoinedVoice())
            {
                await FollowupAsync("Already Joined a Voice Channel");
                return;
            }

            // Get the audio channel
            var channel = (Context.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
            {
                await FollowupAsync("You have to be in a Voice Channel, otherwise Miku can't join you :c");
                return;
            }

            var botPermissions = Context.Guild.CurrentUser.GetPermissions(channel);
            if (!botPermissions.Connect || !botPermissions.Speak)
            {
                await FollowupAsync("I don't have permission to connect or speak in that Voice Channel!");
                return;
            }

            if (!botPermissions.SendMessages)
                _logger.LogWarning("[{Guild}] Missing SendMessages permission in voice text channel '{Channel}' — RespondAsync may not be visible there.",
                    Context.Guild.Name, channel.Name);

            IAudioClient audioClient;
            try
            {
                audioClient = await channel.ConnectAsync();
            }
            catch (TimeoutException)
            {
                await FollowupAsync("Could not connect to the Voice Channel. Please try again.");
                return;
            }

            mikuState.CreateServices(_logger, audioClient, _config);
            mikuState.SetJoinedVoice(true);

            await FollowupAsync($"Joined Voice Channel {channel.Name}! 🎶");
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

        [SlashCommand("add_youtube", "Add a Youtube Link to the Queue")]
        public async Task AddYoutubeAsync(string youtubeUrl)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            if(!YoutubeValidator.IsVideo(youtubeUrl, out var validYoutubeUrl))
            {
                await RespondAsync("Invalid Youtube Link");
                return;
            }

            await DeferAsync();

            var ytSerivce = new YoutubeService();
            var metadata = await ytSerivce.GetMetadata(validYoutubeUrl!.ToString());
            if (!metadata.Success)
            {
                foreach (var error in metadata.ErrorOutput)
                    Console.WriteLine($"[YTDownloader] {error}");
                await FollowupAsync("Invalid Youtube Link");
                return;
            }

            var song = new Song()
            {
                Title = metadata.Data.Title,
                Url = validYoutubeUrl!.ToString()
            };

            mikuState.GetQueueService().AddToQueue(song);

            await FollowupAsync($"Song {song.Title} Added ♪♫");
        }

        [SlashCommand("add_youtube_playlist", "Add a Youtube PLaylist to the Queue LONG LOADING TIME")]
        public async Task AddYoutubePlaylistAsync(string youtubeUrl)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            if(!YoutubeValidator.IsPlaylist(youtubeUrl, out var validYoutubeUrl))
            {
                await RespondAsync("Invalid Youtube Link");
                return;
            }

            await DeferAsync();

            var ytSerivce = new YoutubeService();
            var metadata = await ytSerivce.GetMetadata(validYoutubeUrl!.ToString());
            if (!metadata.Success)
            {
                foreach (var error in metadata.ErrorOutput)
                    Console.WriteLine($"[YTDownloader] {error}");
                await FollowupAsync("Invalid Youtube Link");
                return;
            }

            VideoData[] entries;
            try
            {
                entries = metadata.Data.Entries.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                await FollowupAsync("Internal Error. Contact Developer. Code 100");
                return;
            }

            var songs = entries.Select(e => new Song
            {
                Title = e.Title,
                Url = e.Url
            }).ToList();

            mikuState.GetQueueService().AddToQueue(songs);

            await FollowupAsync($"Playlist {metadata.Data.Title} Added ♪♫ ({songs.Count} songs)");
        }

        [SlashCommand("play", "Play a Youtube Link")]
        public async Task PlayAsync(int? songNumber = null)
        {
            await DeferAsync();
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await FollowupAsync("Something went wrong");
                return;
            }

            if (!mikuState.GetJoinedVoice())
            {
                await FollowupAsync("Miku is not in a Voice Channel");
                return;
            }

            var queueService = mikuState.GetQueueService();

            if (queueService.IsPlaying())
            {
                await FollowupAsync("Miku is alrady playing Music for you. ♪♫");
                return;
            }

            if (queueService.IsEmpty())
            {
                await FollowupAsync("Queue is Empty");
                return;
            }

            if (songNumber is not null)
            {
                if (songNumber <= 0 || songNumber > queueService.GetQueue().Count())
                {
                    await FollowupAsync("Invalid Number.");
                    return;
                }
                else
                    queueService.SetCurrentIndex((int)songNumber - 1);
            }

            queueService.SetMikuAudioService(mikuState.GetAudioService());
            queueService.Play();

            await FollowupAsync($"♪♫");
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

            if (!mikuState.GetQueueService().IsPlaying())
            {
                await RespondAsync("Miku is not playing Music right now.");
                return;
            }

            mikuState.GetAudioService().Stop();
            mikuState.GetQueueService().RemoveMikuAudioService();

            await RespondAsync("Miku stopped playing musik :c");
            await DeleteOriginalResponseAsync();
        }

        [SlashCommand("loop", "Set loop on or off.")]
        public async Task SetLoopAsync(bool loop)
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

        [SlashCommand("set_shuffle", "Set shuffle on or off.")]
        public async Task SetShuffleAsync(bool shuffle)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            queue.SetShuffle(shuffle);
            await RespondAsync($"Shuffle set to {shuffle}");
        }

        [SlashCommand("skip", "Skip the current song")]
        public async Task SkipAsync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            if (!queue.IsPlaying())
            {
                await RespondAsync("Miku is not playing any songs right now");
                return;
            }

            await DeferAsync();
            queue.Skip();
            await FollowupAsync("Skip to next Song ♪♫");
        }

        [SlashCommand("clear_queue", "Obvious no? :P")]
        public async Task ClearQueueASync()
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            if (queue.IsPlaying())
            {
                await RespondAsync("Miku is playing right now. Please stop the music first.");
                return;
            }

            queue.ClearQueue();
            await RespondAsync("Queue Cleared ¿");
        }

        [SlashCommand("remove_song_queue", "Remove a Song from Queue")]
        public async Task RemoveSongQueueAsync(int number)
        {
            var mikuState = MikuStateHandler.GetState(Context.Guild);
            if (mikuState is null)
            {
                await RespondAsync("Something went wrong");
                return;
            }

            var queue = mikuState.GetQueueService();
            if (queue.IsPlaying())
            {
                await RespondAsync("Miku is playing right now. Please stop the music first.");
                return;
            }

            if (number <= 0 || number > queue.GetQueue().Count())
            {
                await RespondAsync("Invalid Number.");
                return;
            }

            var title = queue.GetSongTitle(number - 1);
            queue.Remove(number - 1);
            await RespondAsync($"Removed Song: {title}");

        }
    }
}
