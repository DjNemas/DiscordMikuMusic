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
        
        private static IConfiguration _configuration = null!;
        private static IServiceProvider _serviceProvider = null!;

        private static DiscordService _client = null!;
        private static InteractionHandler _interactionService = null!;

        static async Task Main(string[] args)
        {
            Init();

            var provider = _diService.BuildProvider();
            provider.CreateScope();

            _configuration = provider.GetRequiredService<IConfiguration>();

            _client = provider.GetRequiredService<DiscordService>();            
            _interactionService = provider.GetRequiredService<InteractionHandler>();

            await _client.Run();

            await Task.Delay(-1);
        }

        private static void Init()
        {
            var optionsIS = new InteractionServiceConfig();
            optionsIS.LogLevel = LogSeverity.Debug;

            _diService.AddConfigurationServices();
            _diService.AddDiscordSocketClient();
            _diService.AddInteractionHandler();
        }

        
    }
}
