using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordMikuMusic.Services
{
    public class InteractionHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly InteractionService _interactionService;
        private readonly ILogger<InteractionHandler> _logger;
        private IEnumerable<ModuleInfo> _moduls = null!;

        public InteractionHandler(ILogger<InteractionHandler> logger, IServiceProvider serviceProvider, DiscordSocketClient discordSocketClient)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            _discordSocketClient = discordSocketClient;
            _discordSocketClient.InteractionCreated += InteractionCreated;
            _discordSocketClient.GuildAvailable += RegisterModulsToGuildAsync;

            var options = new InteractionServiceConfig()
            {
                LogLevel = LogSeverity.Warning,
                DefaultRunMode = RunMode.Async
            };
            _interactionService = new InteractionService(discordSocketClient, options);
            _interactionService.Log += Log;
        }

        public async Task RegisterAssemblyModulsAsync()
        {
            _moduls = await _interactionService.AddModulesAsync(typeof(Program).Assembly, _serviceProvider);
        }

        private async Task RegisterModulsToGuildAsync(IGuild guild)
        {
            var guildCommands = await guild.GetApplicationCommandsAsync();
            var existingModules = _interactionService.Modules;

            var commandsToRemove = guildCommands.Where(cmd => !existingModules.Any(mod => mod.SlashCommands.Any(sc => sc.Name == cmd.Name))).ToList();

            foreach (var command in commandsToRemove)
            {
                await command.DeleteAsync();
            }

            await _interactionService.AddModulesToGuildAsync(guild, modules: _moduls.ToArray());
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

        private async Task InteractionCreated(SocketInteraction interaction)
        {
            if (IsInteractionExpired(interaction))
                return;

            var context = new SocketInteractionContext(_discordSocketClient, interaction);
            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }

        /// <summary>
        /// [EN] Checks whether the given interaction is too old to be processed (older than 3 seconds).
        /// Logs a detailed error including local and Discord timestamps if clock skew is detected.
        /// <br/>
        /// [DE] Prüft, ob die angegebene Interaction zu alt ist, um verarbeitet zu werden (älter als 3 Sekunden).
        /// Gibt bei erkanntem Clock-Skew eine detaillierte Fehlermeldung mit lokalem und Discord-Timestamp aus.
        /// <br/><br/>
        /// [DE] <b>Was ist Clock-Skew?</b><br/>
        /// Clock-Skew (dt. Uhrabweichung) bedeutet, dass die lokale Systemuhr dieses Computers von der
        /// Uhrzeit auf Discords Servern abweicht. Discord stempelt jede Interaction mit seiner eigenen Serverzeit.
        /// Ist die lokale Uhr mehr als 3 Sekunden vor oder hinter Discords Zeit, denkt Discord.Net,
        /// die Interaction sei bereits abgelaufen — obwohl sie gerade erst gesendet wurde.
        /// Lösung: Windows-Uhrzeit per NTP synchronisieren (Einstellungen → Zeit and Sprache → "Jetzt synchronisieren").
        /// </summary>
        /// <param name="interaction">
        /// [EN] The incoming Discord interaction to validate.<br/>
        /// [DE] Die eingehende Discord Interaction, die überprüft werden soll.
        /// </param>
        /// <returns>
        /// [EN] <see langword="true"/> if the interaction has expired and should not be processed; otherwise <see langword="false"/>.<br/>
        /// [DE] <see langword="true"/>, wenn die Interaction abgelaufen ist und nicht verarbeitet werden soll; andernfalls <see langword="false"/>.
        /// </returns>
        private bool IsInteractionExpired(SocketInteraction interaction)
        {
            var age = (DateTimeOffset.UtcNow - interaction.CreatedAt).TotalSeconds;
            if (age > 3)
            {
                _logger.LogError(
                    "Interaction {Id} is already {Age:F2}s old before processing. Possible clock skew! (Local UTC: {Local}, Interaction: {Created})",
                    interaction.Id, age, DateTimeOffset.UtcNow, interaction.CreatedAt);
                return true;
            }
            return false;
        }
    }
}
