using Discord;
using Discord.WebSocket;
using System.Collections.Concurrent;

namespace DiscordMikuMusic.Services
{
    public class EmojiReactionService
    {
        private readonly DiscordSocketClient _client;
        // Stores: (GuildId, (MessageId, Page))
        private readonly ConcurrentDictionary<ulong, (ulong MessageId, int Page)> _queueMessages = new();
        private readonly IServiceProvider _serviceProvider;

        public EmojiReactionService(DiscordSocketClient client, IServiceProvider serviceProvider)
        {
            _client = client;
            _serviceProvider = serviceProvider;
            _client.ReactionAdded += OnReactionAddedAsync;
        }

        public void RegisterQueueMessage(ulong guildId, ulong messageId, int page)
        {
            _queueMessages[guildId] = (messageId, page);
        }

        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
        {
            if (reaction.UserId == _client.CurrentUser.Id)
                return;

            // Try to get the guildId from the channel
            var channel = channelCache.Value as SocketGuildChannel;
            if (channel == null)
                return;
            var guildId = channel.Guild.Id;

            if (!_queueMessages.TryGetValue(guildId, out var info))
                return;

            if (reaction.MessageId != info.MessageId)
                return;

            // Get queue and update embed
            var guild = _client.GetGuild(guildId);
            var mikuState = MikuStateHandler.GetState(guild);

            if (mikuState == null) 
                return;

            var queue = mikuState.GetQueueService();
            var allSongs = queue.GetQueue().ToList();

            const int pageSize = 25;
            int totalPages = (int)Math.Ceiling(allSongs.Count / (double)pageSize);
            int currentPage = info.Page;
            int newPage = currentPage;

            if (reaction.Emote.Name == "⬅️" && currentPage > 1)
                newPage = currentPage - 1;
            else if (reaction.Emote.Name == "➡️" && currentPage < totalPages)
                newPage = currentPage + 1;
            else
                return;

            // Remove user reaction to keep UI clean
            var msg = await cacheable.GetOrDownloadAsync();
            await msg.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            newPage = Math.Clamp(newPage, 1, totalPages);
            int start = (newPage - 1) * pageSize;
            var pageSongs = allSongs.Skip(start).Take(pageSize).ToList();

            var builder = new EmbedBuilder();
            builder.WithTitle($"Current Queue (Page {newPage}/{totalPages})\n" +
                $"Loop: {queue.GetLoopState()}\n" +
                $"Shuffle: {queue.GetShuffleState()}");
            builder.WithColor(Color.Blue);
            foreach (var item in pageSongs)
            {
                builder.AddField((queue.GetSongPosition(item) + 1).ToString() +
                    (queue.GetSongPosition(item) == queue.GetCurrentIndex() ? " - Playing" : string.Empty),
                    item.Title);
            }

            await msg.ModifyAsync(m => m.Embed = builder.Build());

            // Remove all old reactions and add new ones
            await msg.RemoveAllReactionsAsync();
            if (totalPages > 1)
            {
                if (newPage > 1)
                {
                    var left = new Emoji("⬅️");
                    await msg.AddReactionAsync(left);
                }
                if (newPage < totalPages)
                {
                    var right = new Emoji("➡️");
                    await msg.AddReactionAsync(right);
                }
            }

            _queueMessages[guildId] = (info.MessageId, newPage);
        }
    }
}
