using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Configuration;
using DiscordMikuMusic.Services;
using Microsoft.Extensions.DependencyInjection;
using Discord.Interactions;
using System.Runtime.CompilerServices;

namespace DiscordMikuMusic
{
    //  Implement Miku Logger later <summary>
    internal class Program
    {
        private static DependencyInjectionService _diService = new DependencyInjectionService();

        static async Task Main(string[] args)
        {
            var scope = _diService.BuildAndCreateScope();
            
            scope.GetRequiredService<InteractionHandler>();

            var client = scope.GetRequiredService<DiscordService>();
            await client.Run();

            await Task.Delay(-1);
        }
        
    }
}
