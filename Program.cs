namespace GatoIRCBot;

using GodotEGPNonGame.ServiceWorkers;

using GatoIRCBot.Config;

using GodotEGP;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Random;
using GodotEGP.Objects.Extensions;
using GodotEGP.Event.Events;
using GodotEGP.Event.Filter;
using Godot;

class Program
{
	public static GodotEGP.Main GodotEGP;

    static async Task<int> Main(string[] args)
    {
		// init GodotEGP
		GodotEGP = new GodotEGP.Main();
		SceneTree.Instance.Root.AddChild(GodotEGP);

		// wait for services to be ready
		if (!ServiceRegistry.WaitForServices(
					typeof(ConfigManager), 
					typeof(ResourceManager), 
					typeof(ScriptService)
					))
			{
			LoggerManager.LogCritical("Required services never became ready");

			return 0;
		}

		LoggerManager.LogInfo("Services ready");

		// create SceneTree service worker instance
		var serviceWorker = new SceneTreeServiceWorker();
		await serviceWorker.StartAsync(new CancellationToken());

		LoggerManager.LogInfo("GodotEGP ready!");

		var ircConfig = ServiceRegistry.Get<ConfigManager>().Get<IRCConfig>();
		LoggerManager.LogDebug("IRC config", "", "ircConfig", ircConfig);

		// wait forever until we close the program
		await Task.Delay(-1);
		return 0;
    }
}
