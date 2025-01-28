using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection.Emit;

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
            _collection.AddSingleton<DiscordService>();
            _collection.AddSingleton<DiscordSocketClient>();

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
    }
}
