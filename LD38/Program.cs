using Microsoft.Extensions.DependencyInjection;

namespace LD38
{
    public class Program
    {
        static void Main(string[] args)
        {
            var provider = new ServiceCollection()
                                .AddOptions()
                                .AddGameService<IUpdateLoopService, UpdateLoopService>()
                                .AddGameService<GlfwService>()
                                .Configure<GlfwOptions>(options => options.Title = "A Small World")
                                .BuildServiceProvider();

            var game = ActivatorUtilities.CreateInstance<Game>(provider);
            var updateLoop = (UpdateLoopService)provider.GetRequiredService<IUpdateLoopService>();

            game.Initialise();

            game.Start();

            while (game.RunState == GameRunState.Running)
            {
                updateLoop.RunFrame();
            }

            game.Stop();
        }
    }
}
