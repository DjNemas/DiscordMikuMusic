using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Reflection.Metadata.Ecma335;

namespace DiscordMikuMusic.Services
{
    public class DiscordService
    {
        private readonly IConfiguration _configuration;
        private readonly DiscordSocketClient _client;

        public DiscordService(IConfiguration configuration, DiscordSocketClient discordSocketClient)
        {
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
            if(state is not null)   
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
            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }
    }
}
