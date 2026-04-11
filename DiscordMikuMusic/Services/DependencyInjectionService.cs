using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMikuMusic.Services
{
    internal class DependencyInjectionService
    {
        private IServiceCollection _collection;

        public DependencyInjectionService()
        {
            _collection = new ServiceCollection();
        }

        public IServiceProvider BuildAndCreateScope()
        {
            // Singletons
            AddDiscorcServiceOption();
            _collection.AddSingleton<DiscordService>();
            _collection.AddSingleton<DiscordSocketClient>();
            _collection.AddSingleton<EmojiReactionService>();

            // Scoped
            AddConfigurationServices();
            _collection.AddScoped<InteractionHandler>();
            _collection.AddScoped<YoutubeService>();
            _collection.AddScoped<MikuAudioService>();

            return _collection.BuildServiceProvider().CreateScope().ServiceProvider;
        }

        private void AddConfigurationServices()
        {
            Console.WriteLine(AppContext.BaseDirectory);
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("settings.json");
            _collection.AddScoped<IConfiguration>(x => builder.Build());
        }

        private void AddDiscorcServiceOption()
        {
            _collection.AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                LogLevel = LogSeverity.Debug,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 1000
            });
        }
    }
}
