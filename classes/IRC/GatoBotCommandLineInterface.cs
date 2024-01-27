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

		_commands["erasemsg"] = (BotCommandEraseMessage, "Erase messages from history", true);
		_commandArgs["erasemsg"] = new();
		_commandArgs["erasemsg"].Add(("n", "N", "Number of messages to erase", false));

		_commands["editmsg"] = (BotCommandEditMessage, "Edit a chat message at position N", true);
		_commandArgs["editmsg"] = new();
		_commandArgs["editmsg"].Add(("n", "N", "N position from last message", false));
		_commandArgs["editmsg"].Add(("msg", "MESSAGE", "New message content", false));
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

	public async Task<int> BotCommandEraseMessage()
	{
		int eraseCount = 0;

		if (_ircCommandParameters.Count > 0)
		{
			eraseCount = Convert.ToInt32(_ircCommandParameters[0]);
		}

		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName);

		if (sourceHistory != null)
		{
			LoggerManager.LogDebug("Erasing chat history", "", "erase", $"network:{_ircNetworkName}, source:{sourceHistory.SourceName}, count:{eraseCount}");

			sourceHistory.EraseLastMessages(eraseCount);

			_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"Erased the last {eraseCount} messages");
		}
		else
		{
			LoggerManager.LogDebug("Failed to get chat history", "", "erase", $"network:{_ircNetworkName}, source:???, count:{eraseCount}");
		}

		return 0;
	}

	public async Task<int> BotCommandEditMessage()
	{
		if (_ircCommandParameters.Count < 2)
		{
			throw new InvalidCommandParametersException(2, 2);
		}

		int editId = Convert.ToInt32(_ircCommandParameters[0]);
		string contentNew = String.Join(" ", _ircCommandParameters.Skip(1));

		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName);

		if (sourceHistory != null)
		{
			LoggerManager.LogDebug("Editing chat message", "", "edit", $"network:{_ircNetworkName}, source:{sourceHistory.SourceName}, old:{sourceHistory.GetLastMessages(editId + 1).Last().Content}, new:{contentNew}");

			sourceHistory.EditMessage(editId, contentNew);

			_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"Edited message ID {editId} to: {contentNew}");
		}
		else
		{
			LoggerManager.LogDebug("Failed to get chat history", "", "erase", $"network:{_ircNetworkName}, source:???");
		}

		return 0;
	}
}

