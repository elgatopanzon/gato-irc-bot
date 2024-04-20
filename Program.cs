namespace GatoIRCBot;

using GodotEGPNonGame.ServiceWorkers;

using GatoIRCBot.Config;
using GatoIRCBot.IRC;

using GodotEGP;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Random;
using GodotEGP.Objects.Extensions;
using GodotEGP.Event.Events;
using GodotEGP.Event.Filters;
using Godot;

using System.Net;

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

		var ircBotConfig = ServiceRegistry.Get<ConfigManager>().Get<IRCBotConfig>();
		LoggerManager.LogDebug("IRC bot config", "", "ircBotConfig", ircBotConfig);

		Gato ircBot = new Gato(ircConfig, ircBotConfig);
		ircBot.Connect();

		// wait forever until we close the program
		await Task.Delay(-1);
		return 0;
    }
}
