using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DiscordMikuMusic.Services
{
    public class DiscordService
    {
        private readonly ILogger<DiscordService> _logger;
        private readonly IConfiguration _configuration;
        private readonly DiscordSocketClient _client;

        public DiscordService(ILogger<DiscordService> logger, IConfiguration configuration, DiscordSocketClient discordSocketClient)
        {
            _logger = logger;
            _configuration = configuration;
            _client = discordSocketClient;
            _client.Log += Log;
            _client.GuildAvailable += GuildAvailable;
            _client.GuildUnavailable += GuildUnavailable;
        }

        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, _configuration.GetSection("DiscordBot:TokenDebug").Value!);
            await _client.StartAsync();
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            var state = MikuStateHandler.GetState(guild);
            if (state is not null)
                MikuStateHandler.RemoveState(state);
            return Task.CompletedTask;
        }

        private Task GuildAvailable(SocketGuild guild)
        {
            MikuStateHandler.CreateState(guild);
            return Task.CompletedTask;
        }

        private Task Log(LogMessage message)
        {
            var text = $"[{message.Source}] {message.Message ?? message.Exception?.Message ?? "(no message)"}";
            if (message.Severity == LogSeverity.Error)
                _logger.LogError(message.Exception, text);
            else if (message.Severity == LogSeverity.Warning)
                _logger.LogWarning(message.Exception, text);
            else if (message.Severity == LogSeverity.Debug)
                _logger.LogDebug(message.Exception, text);
            else
                _logger.LogInformation(message.Exception, text);

            return Task.CompletedTask;
        }
    }
}
