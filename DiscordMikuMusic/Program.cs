using System.Text;
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
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            IServiceProvider scope = TryBuildAndCreateScope()!;

            var updater = scope.GetRequiredService<IYTDLPUpdaterService>();
            await updater.TryUpdate();

            var daveUpdater = scope.GetRequiredService<ILibDaveUpdaterService>();
            await daveUpdater.TryUpdate();

            var ffmpegUpdater = scope.GetRequiredService<IFFmpegUpdaterService>();
            await ffmpegUpdater.TryUpdate();

            var sodiumUpdater = scope.GetRequiredService<ILibSodiumUpdaterService>();
            await sodiumUpdater.TryUpdate();

            var opusUpdater = scope.GetRequiredService<ILibOpusUpdaterService>();
            await opusUpdater.TryUpdate();

            var interactionService = scope.GetRequiredService<InteractionHandler>();
            await interactionService.RegisterAssemblyModulsAsync();

            var client = scope.GetRequiredService<DiscordService>();
            await client.Run();

            await Task.Delay(-1);
        }

        private static IServiceProvider? TryBuildAndCreateScope()
        {
            try
            {
                return _diService.BuildAndCreateScope();
            }
            catch (InvalidOperationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[Fatal] Configuration error: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
            return null;
        }
    }
}
