using DiscordMikuMusic.Interfaces;
using DiscordMikuMusic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordMikuMusic
{
    //  Implement Miku Logger later <summary>
    internal class Program
    {
        private static DependencyInjectionService _diService = new DependencyInjectionService();

        static async Task Main(string[] args)
        {
            var scope = _diService.BuildAndCreateScope();

            var updater = scope.GetRequiredService<IYTDLPUpdaterService>();
            await updater.TryUpdate();

            var interactionService = scope.GetRequiredService<InteractionHandler>();
            await interactionService.RegisterAssemblyModulsAsync();

            var client = scope.GetRequiredService<DiscordService>();
            await client.Run();

            await Task.Delay(-1);
        }
    }
}
