using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordMikuMusic.Interactions.SlashCommands;
using Microsoft.VisualBasic;
using System.Reactive.Threading.Tasks;

namespace DiscordMikuMusic.Services
{
    public class InteractionHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly InteractionService _interactionService;
        private IEnumerable<ModuleInfo> _moduls = null!;

        public InteractionHandler(IServiceProvider serviceProvider, DiscordSocketClient discordSocketClient)
        {
            _serviceProvider = serviceProvider;

            _discordSocketClient = discordSocketClient;
            _discordSocketClient.InteractionCreated += InteractionCreated;
            _discordSocketClient.GuildAvailable += RegisterCommandsToGuildAsync;

            var options = new InteractionServiceConfig()
            {
                LogLevel = LogSeverity.Debug
            };
            _interactionService = new InteractionService(discordSocketClient, options);
            RegisterModulsAsync();
        }

        public async Task RegisterCommandsToGuildAsync(IGuild guild)
        {
            await _interactionService.AddModulesToGuildAsync(guild, modules: _moduls.ToArray());
        }

        private async Task InteractionCreated(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_discordSocketClient, interaction);

            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }

        private async void RegisterModulsAsync()
        {
            _moduls = await _interactionService.AddModulesAsync(typeof(Program).Assembly, _serviceProvider);
        }
    }
}
