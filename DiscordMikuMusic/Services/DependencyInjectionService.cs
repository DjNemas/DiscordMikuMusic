using System.Runtime.InteropServices;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Models;
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
        private IConfiguration _config = null!;

        public DependencyInjectionService()
        {
            _collection = new ServiceCollection();
        }

        /// <summary>
        /// [EN] Registers all services into the DI container, builds the <see cref="IServiceProvider"/>,
        /// and returns a new child scope's <see cref="IServiceProvider"/> for use throughout the application lifetime.
        /// <br/>
        /// [DE] Registriert alle Services im DI-Container, baut den <see cref="IServiceProvider"/> auf
        /// und gibt den <see cref="IServiceProvider"/> eines neuen Child-Scopes zurück,
        /// der für die gesamte Laufzeit der Anwendung verwendet wird.
        /// </summary>
        /// <returns>
        /// [EN] A scoped <see cref="IServiceProvider"/> with all registered services available.<br/>
        /// [DE] Ein scope-basierter <see cref="IServiceProvider"/> mit allen registrierten Services.
        /// </returns>
        public IServiceProvider BuildAndCreateScope()
        {
            // Native-Library-Resolver registrieren, bevor Discord.Net opus lädt
            RegisterLibOpusResolver();

            // Konfiguration zuerst laden – alle Build-Methoden setzen _config voraus
            AddConfigurationServices();

            // Singletons – einmalig für die gesamte App-Laufzeit erstellt
            _collection.AddSingleton<IYTDLPUpdaterService, YTDLPUpdaterService>();
            _collection.AddSingleton<ILibDaveUpdaterService, LibDaveUpdaterService>();
            _collection.AddSingleton<IFFmpegUpdaterService, FFMPEGUpdateService>();
            _collection.AddSingleton<ILibSodiumUpdaterService, LibSodiumUpdaterService>();
            _collection.AddSingleton<ILibOpusUpdaterService, LibOpusUpdaterService>();
            _collection.AddSingleton(factory => new DiscordSocketClient(GetDiscordClientOptions()));
            _collection.AddSingleton<DiscordService>();
            _collection.AddSingleton<EmojiReactionService>();
            _collection.AddSingleton(GetInteractionServiceConfig());

            // Scoped – pro Scope einmal erstellt (hier: ein einziger App-Scope)
            _collection.AddScoped<InteractionHandler>();
            _collection.AddScoped<YoutubeService>();
            _collection.AddScoped<MikuAudioService>();

            // MikuLogger – ersetzt alle Standard-Provider durch den eigenen strukturierten Logger
            _collection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddMikuLogger(GetMikuLoggerOptions());
            });

            // Scope erstellen, damit Scoped-Services über den gesamten App-Lifetime aufgelöst werden können
            return _collection.BuildServiceProvider().CreateScope().ServiceProvider;
        }

        /// <summary>
        /// [EN] Creates and returns the configuration options for the MikuLogger.
        /// <c>Logging:MinimumLogLevel</c> is read from <c>settings.json</c>; the application throws if the key is missing or invalid.
        /// <br/>
        /// [DE] Erstellt und gibt die Konfigurationsoptionen für den MikuLogger zurück.
        /// <c>Logging:MinimumLogLevel</c> wird aus <c>settings.json</c> gelesen; fehlt der Schlüssel oder ist er ungültig, wirft die Anwendung eine Exception.
        /// </summary>
        /// <returns>
        /// [EN] A configured <see cref="MikuLoggerOptions"/> instance.<br/>
        /// [DE] Eine konfigurierte <see cref="MikuLoggerOptions"/>-Instanz.
        /// </returns>
        private MikuLoggerOptions GetMikuLoggerOptions()
        {
            var levelStr = _config["Logging:MinimumLogLevel"]
                ?? throw new InvalidOperationException("'Logging:MinimumLogLevel' is not configured in settings.json.");

            if (!Enum.TryParse<MikuLogLevel>(levelStr, ignoreCase: true, out var minLevel))
                throw new InvalidOperationException($"'Logging:MinimumLogLevel' has an invalid value '{levelStr}'. Valid values: {string.Join(", ", Enum.GetNames<MikuLogLevel>())}");

            return new MikuLoggerOptions
            {
                Output = MikuLogOutput.ConsoleAndFile, // Console, File, and SSE
                MinimumLogLevel = minLevel,
                DateFormat = Strings.LogDateTimeFormat,
                UseUtcTime = false,
                FormatOptions = new MikuLogFormatOptions
                {
                    ShowDate = true,
                    ShowLoggerName = true,
                    ShowLogLevel = true,
                    ShowTime = true,
                },

                // File options
                FileOptions = new MikuFileLoggerOptions
                {
                    LogDirectory = "./logs",
                    FileNamePattern = "app.log",
                    MaxFileSizeBytes = 10 * 1024 * 1024, // 10 MB
                    MaxFileCount = 25,
                    UseDateFolders = true,
                    DateFolderFormat = Strings.LogDateFolderFormat
                },

                ConsoleColors = new MikuConsoleColorOptions
                {
                    Enabled = true,
                    ColorSpace = MikuColorSpace.TrueColor
                }
            };
        }

        /// <summary>
        /// [EN] Creates and returns the configuration for the <see cref="DiscordSocketClient"/>.
        /// <c>Discord:LogLevel</c> is read from <c>settings.json</c>; the application throws if the key is missing or invalid.
        /// Only the gateway intents that are actively used by the bot are enabled
        /// to avoid Discord warnings about unused intents.
        /// <br/>
        /// [DE] Erstellt und gibt die Konfiguration für den <see cref="DiscordSocketClient"/> zurück.
        /// <c>Discord:LogLevel</c> wird aus <c>settings.json</c> gelesen; fehlt der Schlüssel oder ist er ungültig, wirft die Anwendung eine Exception.
        /// Es werden nur die Gateway-Intents aktiviert, die vom Bot tatsächlich genutzt werden,
        /// um Warnungen von Discord über ungenutzte Intents zu vermeiden.
        /// </summary>
        /// <returns>
        /// [EN] A configured <see cref="DiscordSocketConfig"/> instance.<br/>
        /// [DE] Eine konfigurierte <see cref="DiscordSocketConfig"/>-Instanz.
        /// </returns>
        private DiscordSocketConfig GetDiscordClientOptions()
        {
            var levelStr = _config["Discord:LogLevel"]
                ?? throw new InvalidOperationException("'Discord:LogLevel' is not configured in settings.json.");

            if (!Enum.TryParse<LogSeverity>(levelStr, ignoreCase: true, out var logLevel))
                throw new InvalidOperationException($"'Discord:LogLevel' has an invalid value '{levelStr}'. Valid values: {string.Join(", ", Enum.GetNames<LogSeverity>())}");

            return new DiscordSocketConfig()
            {
                // Guilds              – GuildAvailable / GuildUnavailable / InteractionCreated
                // GuildVoiceStates    – Voice-Channel-Tracking für den Music-Bot
                // GuildMessageReactions – ReactionAdded im EmojiReactionService
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMessageReactions,
                LogLevel = logLevel,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 1000,
                EnableVoiceDaveEncryption = true
            };
        }

        /// <summary>
        /// [EN] Creates and returns the <see cref="InteractionServiceConfig"/> by reading
        /// <c>Discord:Interaction:LogLevel</c> and <c>Discord:Interaction:DefaultRunMode</c> from <c>settings.json</c>.
        /// Registered as a singleton so it can be injected into <see cref="InteractionHandler"/>.
        /// <br/>
        /// [DE] Erstellt und gibt die <see cref="InteractionServiceConfig"/> zurück, indem
        /// <c>Discord:Interaction:LogLevel</c> und <c>Discord:Interaction:DefaultRunMode</c> aus <c>settings.json</c> gelesen werden.
        /// Als Singleton registriert, damit sie in den <see cref="InteractionHandler"/> injiziert werden kann.
        /// </summary>
        /// <returns>
        /// [EN] A configured <see cref="InteractionServiceConfig"/> instance.<br/>
        /// [DE] Eine konfigurierte <see cref="InteractionServiceConfig"/>-Instanz.
        /// </returns>
        private InteractionServiceConfig GetInteractionServiceConfig()
        {
            var levelStr = _config["Discord:Interaction:LogLevel"]
                ?? throw new InvalidOperationException("'Discord:Interaction:LogLevel' is not configured in settings.json.");

            if (!Enum.TryParse<LogSeverity>(levelStr, ignoreCase: true, out var logLevel))
                throw new InvalidOperationException($"'Discord:Interaction:LogLevel' has an invalid value '{levelStr}'. Valid values: {string.Join(", ", Enum.GetNames<LogSeverity>())}");

            var runModeStr = _config["Discord:Interaction:DefaultRunMode"]
                ?? throw new InvalidOperationException("'Discord:Interaction:DefaultRunMode' is not configured in settings.json.");

            if (!Enum.TryParse<RunMode>(runModeStr, ignoreCase: true, out var runMode))
                throw new InvalidOperationException($"'Discord:Interaction:DefaultRunMode' has an invalid value '{runModeStr}'. Valid values: {string.Join(", ", Enum.GetNames<RunMode>())}");

            return new InteractionServiceConfig()
            {
                LogLevel = logLevel,
                DefaultRunMode = runMode
            };
        }

        /// <summary>
        /// [EN] Registers a <see cref="NativeLibrary.SetDllImportResolver"/> for the Discord.Net WebSocket assembly
        /// so that the native library name <c>opus</c> is remapped to <c>libopus</c>.
        /// <br/>
        /// [DE] Registriert einen <see cref="NativeLibrary.SetDllImportResolver"/> für die Discord.Net-WebSocket-Assembly,
        /// damit der native Library-Name <c>opus</c> auf <c>libopus</c> umgeleitet wird.
        /// </summary>
        private static void RegisterLibOpusResolver()
        {
            var discordAssembly = typeof(DiscordSocketClient).Assembly;

            NativeLibrary.SetDllImportResolver(discordAssembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName == "opus")
                    return NativeLibrary.Load("libopus", assembly, searchPath);

                return nint.Zero;
            });
        }

        /// <summary>
        /// [EN] Builds <c>settings.json</c> into <see cref="_config"/>, registers <see cref="IConfiguration"/> as scoped,
        /// and registers <see cref="AppInfo"/> as a singleton.
        /// Throws <see cref="InvalidOperationException"/> if any required key is absent, so the application
        /// cannot start with an incomplete configuration.
        /// <br/>
        /// [DE] Liest <c>settings.json</c> in <see cref="_config"/>, registriert <see cref="IConfiguration"/> als Scoped-Service
        /// und <see cref="AppInfo"/> als Singleton.
        /// Wirft eine <see cref="InvalidOperationException"/>, wenn ein Pflichtschlüssel fehlt, damit die Anwendung
        /// nicht mit einer unvollständigen Konfiguration startet.
        /// </summary>
        private void AddConfigurationServices()
        {
            Console.WriteLine(AppContext.BaseDirectory);
            _config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("settings.json")
                .Build();

            _collection.AddScoped<IConfiguration>(_ => _config);
            _collection.AddSingleton(new AppInfo(
                _config["App:Name"] ?? throw new InvalidOperationException("'App:Name' is not configured in settings.json."),
                _config["App:Version"] ?? throw new InvalidOperationException("'App:Version' is not configured in settings.json.")
            ));
        }

        }
}
