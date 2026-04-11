using Discord;
using Discord.WebSocket;
using DiscordMikuMusic.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Miku.Logger.Configuration;
using Miku.Logger.Configuration.Enums;
using Miku.Logger.Configuration.Models;
using Miku.Logger.Extensions;

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
            _collection.AddSingleton<IYTDLPUpdaterService, YTDLPUpdaterService>();
            _collection.AddSingleton(factory => new DiscordSocketClient(GetDiscordCLientOptions()));
            _collection.AddSingleton<DiscordService>();
            _collection.AddSingleton<EmojiReactionService>();

            // Scoped
            AddConfigurationServices();
            _collection.AddScoped<InteractionHandler>();
            _collection.AddScoped<YoutubeService>();
            _collection.AddScoped<MikuAudioService>();

            // MikuLogger
            _collection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddMikuLogger(GetMikuLoggerOptions());
            });

            return _collection.BuildServiceProvider().CreateScope().ServiceProvider;
        }

        private MikuLoggerOptions GetMikuLoggerOptions()
        {
            return new MikuLoggerOptions
            {
                Output = MikuLogOutput.ConsoleAndFile, // Console, File, and SSE
                MinimumLogLevel = MikuLogLevel.Trace,
                DateFormat = "yyyy-MM-dd HH:mm:ss.fff",
                UseUtcTime = false,

                // File options
                FileOptions = new MikuFileLoggerOptions
                {
                    LogDirectory = "./logs",
                    FileNamePattern = "app.log",
                    MaxFileSizeBytes = 10 * 1024 * 1024, // 10 MB
                    MaxFileCount = 25,
                    UseDateFolders = true,
                    DateFolderFormat = "yyyy-MM-dd"
                },

                ConsoleColors = new MikuConsoleColorOptions
                {
                    Enabled = true,
                    ColorSpace = MikuColorSpace.TrueColor
                }

            };
        }

        private DiscordSocketConfig GetDiscordCLientOptions()
        {
            return new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildVoiceStates,
                LogLevel = LogSeverity.Warning,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 1000
            };
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
