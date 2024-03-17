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

using System.Net.Http;

using Newtonsoft.Json;

public partial class GatoBotCommandLineInterface : IRCBotCommandLineInterface
{
	private new Gato _ircBot;

	public GatoBotCommandLineInterface(Gato ircBot) : base(ircBot)
	{
		_ircBot = ircBot;

		_commands["profile"] = (BotCommandProfile, "Switch currently active model profile", true);
		_commandArgs["profile"] = new();
		_commandArgs["profile"].Add(("set", $"{_ircBot.CommandPrefix}profile set model-profile-id", "Profile ID to switch to (list with 'list')", false));
		_commandArgs["profile"].Add(("list", $"{_ircBot.CommandPrefix}profile list", "List profiles", false));

		_commands["erasemsg"] = (BotCommandEraseMessage, "Erase messages from history", true);
		_commandArgs["erasemsg"] = new();
		_commandArgs["erasemsg"].Add(("N", $"{_ircBot.CommandPrefix}erasemsg 1", "Number of messages to erase", false));

		_commands["editmsg"] = (BotCommandEditMessage, "Edit a chat message at position N", true);
		_commandArgs["editmsg"] = new();
		_commandArgs["editmsg"].Add(("N", "1", "N position from last message", false));
		_commandArgs["editmsg"].Add(("msg", $"{_ircBot.CommandPrefix}editmsg 1 This is a message", "New message content", false));

		_commands["reloadhistory"] = (BotCommandReloadHistory, "Reload the chat history from file", true);
		_commands["erasehistory"] = (BotCommandEraseHistory, "Erase the chat history (permanent!)", true);

		_commands["stop"] = (BotCommandStop, "Stop generating", true);
		_commands["regenerate"] = (BotCommandRegenerate, "Remove 1 message and regenerate", true);
		_commands["continue"] = (BotCommandContinue, "Continue generation", true);
		_commands["recontinue"] = (BotCommandRecontinue, "Remove previous message, and continue", true);

		_commands["speak"] = (BotCommandSpeak, "Speak to a target", false);
		_commandArgs["speak"] = new();
		_commandArgs["speak"].Add(("target", "NickServ", "The target to speak to", false));
		_commandArgs["speak"].Add(("msg", $"{_ircBot.CommandPrefix}speak NickServ Hello there from a bot!", "Message content to send to the target", false));

		_commands["tokenize"] = (BotCommandTokenize, "Tokenise a string", true);

		_commands["paste"] = (BotCommandPaste, "Send message from URL paste", true);
		_commandArgs["paste"] = new();
		_commandArgs["paste"].Add(("url", "https://somesite.com/text", "The URL to load paste from", true));

		_commands["session"] = (BotCommandSession, "List or set the name of the session history", true);
		_commandArgs["session"] = new();
		_commandArgs["session"].Add(("name", "MySession", "The name of the session to change to", true));
	}

	public async Task<int> BotCommandProfile()
	{
		// if nothing is provided, list current model profiles
		if (_ircCommandParameters.Count == 0)
		{
			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Active profile: {_ircBot.Config.ModelProfileId}");

			PrintModelProfiles(_ircBot.Config.ModelProfileId);
		}
		else {
			string profileCommand = _ircCommandParameters[0];

			if (profileCommand == "list")
			{
				PrintModelProfiles();
			}
			else if (profileCommand == "set" && _ircCommandParameters.Count >= 2)
			{
				string newModelProfile = _ircCommandParameters[1];

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
		}

		return 0;
	}

	public async void PrintModelProfiles(string match = "")
	{
		foreach (var modelProfile in _ircBot.Config.ModelProfiles)
		{
			// skip if match is set and it doesn't match
			if (match.Length > 0 && modelProfile.Key != match)
			{
				continue;
			}

			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"{modelProfile.Key} ({modelProfile.Value.Inference.Model} - {modelProfile.Value.HistoryTokenSize} max Ts)");
			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"  freqpen:{modelProfile.Value.Inference.FrequencyPenalty}, prespen:{modelProfile.Value.Inference.PresencePenalty}, topp:{modelProfile.Value.Inference.Temperature}, temp:{modelProfile.Value.Inference.Temperature}");

			var systemPrompts = _ircBot.Config.DefaultSystemPrompts;
			if (modelProfile.Value.SystemPrompts.Count > 0)
			{
				systemPrompts = modelProfile.Value.SystemPrompts;
			}
			systemPrompts = systemPrompts.Concat(_ircBot.Config.AdditionalSystemPrompts).ToList();

			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"  sysprompt:{JsonConvert.SerializeObject(systemPrompts)}");

			// cfg negative prompt
			if (modelProfile.Value.UseGatoGPTExtended && modelProfile.Value.Extended != null && modelProfile.Value.Extended.Inference != null && modelProfile.Value.Extended.Inference.CfgNegativePrompt != null)
			{
				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"  cfgscale:{modelProfile.Value.Extended.Inference.CfgScale}, cfgprompt:\"{modelProfile.Value.Extended.Inference.CfgNegativePrompt}\"");
			}
		}
	}

	public async Task<int> BotCommandEraseMessage()
	{
		int eraseCount = 0;

		if (_ircCommandParameters.Count > 0)
		{
			eraseCount = Convert.ToInt32(_ircCommandParameters[0]);
		}

		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

		if (sourceHistory != null)
		{
			LoggerManager.LogDebug("Erasing chat history", "", "erase", $"network:{_ircNetworkName}, source:{sourceHistory.SourceName}, count:{eraseCount}");

			sourceHistory.EraseLastMessages(eraseCount);
			_ircBot.EraseLastChatHistoryMessages(sourceHistory, eraseCount);

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

		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

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

	public async Task<int> BotCommandReloadHistory()
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

		if (sourceHistory != null)
		{
			_ircBot.ReloadMessageHistoryForClientSource(_ircClient, _ircNetworkName, sourceHistory.SourceName);

			LoggerManager.LogDebug("Cleared loaded history, will be reloaded", "", "reloadHistory", $"network:{sourceHistory.NetworkName}, source:{sourceHistory.SourceName}");

			_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"History reloaded for session \"{sourceHistory.SessionName}\"");
		}

		return 0;
	}

	public async Task<int> BotCommandEraseHistory()
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

		if (sourceHistory != null)
		{
			_ircBot.EraseMessageHistoryForClientSource(_ircNetworkName, sourceHistory);
			_ircBot.ReloadMessageHistoryForClientSource(_ircClient, _ircNetworkName, sourceHistory.SourceName);

			LoggerManager.LogDebug("Erased saved chat history", "", "eraseHistory", $"network:{sourceHistory.NetworkName}, source:{sourceHistory.SourceName}");

			_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"History erased for session \"{sourceHistory.SessionName}\"");
		}

		return 0;
	}

	public async Task<int> BotCommandStop()
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

		_ircBot.StopGeneration(sourceHistory);

		_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"Stopping generation");

		return 0;
	}

	public async Task<int> BotCommandRegenerate()
	{
		TriggerRegeneration(1);

		_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"Regenerating response");

		return 0;
	}

	public async Task<int> BotCommandContinue()
	{
		var modelProfile = _ircBot.Config.ModelProfile;
		string? genTemplate = null;

		if (modelProfile.UseGatoGPTExtended)
		{
			if (modelProfile.Extended == null)
			{
				modelProfile.Extended = new();
			}
			if (modelProfile.Extended.Inference == null)
			{
				modelProfile.Extended.Inference = new();
			}

			// backup current generation template, set it to empty then restore it
			genTemplate = modelProfile.Extended.Inference.ChatMessageGenerationTemplate;
			modelProfile.Extended.Inference.ChatMessageGenerationTemplate = "";
		}

		TriggerRegeneration(0);

		if (modelProfile.UseGatoGPTExtended)
			modelProfile.Extended.Inference.ChatMessageGenerationTemplate = genTemplate;

		_ircClient.LocalUser.SendNotice(_ircReplyTarget, $"Continuing response");

		return 0;
	}

	public async Task<int> BotCommandRecontinue()
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));
		sourceHistory.EraseLastMessages(1);

		await BotCommandContinue();

		return 0;
	}

	public void TriggerRegeneration(int eraseCount = 0)
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));

		sourceHistory.EraseLastMessages(eraseCount);
		_ircBot.QueueOpenAIChatCompletionsRequest(_ircClient, _ircReplyTarget, sourceHistory, sourceHistory.ChatMessages.Last());
	}

	public async Task<int> BotCommandSpeak()
	{
		if (!IsAdmin()) { throw new UserNotAdminException(); }

		if (_ircCommandParameters.Count >= 2)
		{
			string target = _ircCommandParameters[0];
			string msg = String.Join(" ", _ircCommandParameters.Skip(1));

			LoggerManager.LogDebug("Sending message to target", "", target, msg);

			_ircClient.LocalUser.SendMessage(target, msg);
		}

		return 0;
	}

	public async Task<int> BotCommandTokenize()
	{
		if (_ircCommandParameters.Count() >= 1)
		{
			string content = String.Join(" ", _ircCommandParameters);

			var tokenized = _ircBot.GatoGPTTokenizeString(content);

			string formattedTokens = String.Join(" | ", tokenized.Result.Where(x => x.Token.Length > 0).Select(x => $"{x.Id} : '{x.Token}'"));

			_ircClient.LocalUser.SendMessage(_ircReplyTarget, formattedTokens);
		}
		else
		{
			_ircClient.LocalUser.SendNotice(_ircReplyTarget, "tokenize: no string provided!");
		}

		return 0;
	}

	public async Task<int> BotCommandPaste()
	{
		if (_ircCommandParameters.Count() >= 1)
		{
			string contentUrl = _ircCommandParameters[0];

			LoggerManager.LogDebug("Load URL text content as message", "", "url", contentUrl);

			string urlContent = "";
			using (HttpClient client = new HttpClient())
			{
    			urlContent = await client.GetStringAsync(contentUrl);
    			urlContent = urlContent.Replace("\n", ". ");
			}

			LoggerManager.LogDebug("URL text content loaded", "", "content", urlContent);

			// add the additional text to the url content if there's more than
			// just the URL in the message
			if (_ircCommandParameters.Count > 1)
			{
				urlContent += "\n"+String.Join(" ", _ircCommandParameters.Skip(1));
			}

			// forward the loaded url text comment as an incoming message
			_ircBot.ProcessIncomingMessage(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, urlContent, isChannel:(String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false), isHighlight:true, isChatCommand:false);
		}
		else
		{
			_ircClient.LocalUser.SendNotice(_ircReplyTarget, "paste: no URL provided!");
		}

		return 0;
	}

	public async Task<int> BotCommandSession()
	{
		var sourceHistory = _ircBot.GetHistoryFromClient(_ircClient, _ircMessageSource, _ircMessageTargets, _ircNetworkName, (String.Join(" ", _ircMessageTargets).StartsWith("#") ? true : false));
    	string chatSavePath = _ircBot.GetChatMessagesSavePath(sourceHistory);

    	List<string> sessionList = Directory.GetFiles(chatSavePath, "*.log").Select((x, y) => x.GetFile().Replace(".log", "")).ToList();

    	if (!sessionList.Contains(sourceHistory.SessionName))
    	{
    		sessionList.Add(sourceHistory.SessionName);
    	}

		if (_ircCommandParameters.Count() >= 1)
		{
			string sessionName = "";

			try
			{
				// load existing session by id
				int sessionId = Convert.ToInt32(String.Join(" ", _ircCommandParameters));

				sessionName = sessionList[sessionId-1];
			}
			catch (System.Exception)
			{
				// create new session
				sessionName = String.Join(" ", _ircCommandParameters);

				if (sessionList.Contains(sessionName))
				{
					_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Switching to session \"{sessionName}\"");
				}
				else
				{
					_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Creating new session \"{sessionName}\"");
				}
			}

			sourceHistory.SessionName = sessionName;

			BotCommandReloadHistory();

			LoggerManager.LogDebug("Load history session", String.Join(" ", _ircReplyTarget), "session", sessionName);
		}
		else
		{
			_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"Current session: {sourceHistory.SessionName}");

			int sessionId = 1;
			foreach (var sessionName in sessionList)
			{
				_ircClient.LocalUser.SendMessage(_ircReplyTarget, $"{sessionId}: {sessionName}");
				sessionId++;
			}
		}

		return 0;
	}
}

