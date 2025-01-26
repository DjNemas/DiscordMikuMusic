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

        public ServiceProvider BuildProvider()
        {
            return _collection.BuildServiceProvider();
        }

        public void AddDiscordSocketClient()
        {
            _collection.AddSingleton<DiscordService>();
            _collection.AddSingleton<DiscordSocketClient>();
        }

        public void AddConfigurationServices()
        {
            Console.WriteLine(AppContext.BaseDirectory);
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("settings.json");
            _collection.AddScoped<IConfiguration>(x => builder.Build());
        }

        public void AddInteractionHandler()
        {
            _collection.AddScoped<InteractionHandler>();
        }

        public void AddYoutubeService()
        {
            _collection.AddScoped<YoutubeService>();
        }
    }
}
