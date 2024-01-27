/**
 * @author      : ElGatoPanzon (contact@elgatopanzon.io) Copyright (c) ElGatoPanzon
 * @file        : GatoBotCommandLineInterface
 * @created     : Friday Jan 26, 2024 23:06:11 CST
 */

namespace GatoIRCBot.IRC;

using GatoIRCBot.Config;

using Godot;
using GodotEGP.Objects.Extensions;
using GodotEGP.Objects.Validated;
using GodotEGP.Logging;
using GodotEGP.Service;
using GodotEGP.Event.Events;
using GodotEGP.Config;

using GodotEGP.CLI;

using Newtonsoft.Json;

public partial class GatoBotCommandLineInterface : IRCBotCommandLineInterface
{
	private new Gato _ircBot;

	public GatoBotCommandLineInterface(Gato ircBot) : base(ircBot)
	{
		_ircBot = ircBot;

		_commands["profile"] = (BotCommandProfile, "Switch currently active model profile", true);
		_commandArgs["profile"] = new();
		_commandArgs["profile"].Add(("profile_name", "PROFILE_ID", "Profile ID to switch to", false));
	}

	public async Task<int> BotCommandProfile()
	{
		if (!IsAdmin()) { throw new UserNotAdminException(); }

		// if nothing is provided, list current model profiles
		if (_ircCommandParameters.Count == 0)
		{
			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Active profile: {_ircBot.Config.ModelProfileId}");

			_ircClient.LocalUser.SendMessage(_ircReplyTarget, "Available model profiles:");

			foreach (var modelProfile in _ircBot.Config.ModelProfiles)
			{
				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"{modelProfile.Key} ({modelProfile.Value.Inference.Model} - {modelProfile.Value.HistoryTokenSize} max Ts)");
				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"  freqpen:{modelProfile.Value.Inference.FrequencyPenalty}, prespen:{modelProfile.Value.Inference.PresencePenalty}, topp:{modelProfile.Value.Inference.Temperature}, temp:{modelProfile.Value.Inference.Temperature}");

				if (modelProfile.Value.SystemPrompts.Count > 0)
				{
					_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"  sysprompt:{JsonConvert.SerializeObject(modelProfile.Value.SystemPrompts)}");
				}
			}
		}
		else {
			string newModelProfile = _ircCommandParameters[0];

			if (_ircBot.Config.ModelProfiles.TryGetValue(newModelProfile, out var modelProfile))
			{
				_ircBot.Config.ModelProfileId = newModelProfile;

				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Active model profile set to '{newModelProfile}'");
			}
			else
			{
				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Invalid model profile: '{newModelProfile}'");
			}
		}

		return 0;
	}
}

